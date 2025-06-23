const lobbyCode = window.lobbyCode;
const currentPlayerName = window.currentPlayerName;
const isReferee = window.isReferee;
const requestVerificationToken = window.requestVerificationToken;

// Check if the page was reloaded due to round end to prevent loops
const roundOverReloaded = sessionStorage.getItem('roundOverReloaded');
if (roundOverReloaded) {
    sessionStorage.removeItem('roundOverReloaded');
}

const connection = new signalR.HubConnectionBuilder()
    .withUrl("/GameHub")
    .build();

let gameUpdateInterval;

// SignalR Event Listeners
connection.on("UpdateGame", () => fetchAndUpdateUI());
connection.on("NewRoundStarted", () => {
    sessionStorage.removeItem('roundOverReloaded'); // Clear flag before new round
    location.reload();
});
connection.on("GameReset", () => {
    window.location.href = `/Lobby/LobbyRoom?code=${lobbyCode}`;
});

// Fetch and UI Update Logic
async function fetchAndUpdateUI() {
    try {
        const response = await fetch(`/Game/GetGameState?code=${lobbyCode}`);
        if (!response.ok) throw new Error('Network response was not ok');
        
        const state = await response.json();
        if (state.success) {
            updateUI(state);
        } else {
            if (gameUpdateInterval) clearInterval(gameUpdateInterval);
            window.location.href = `/Lobby/LobbyRoom?code=${lobbyCode}`;
        }
    } catch (error) {
        console.error("Error fetching game state:", error);
        if (gameUpdateInterval) clearInterval(gameUpdateInterval);
    }
}

function updateUI(state) {
    // Update scores
    document.getElementById('team1Score').textContent = state.team1Score;
    document.getElementById('team2Score').textContent = state.team2Score;
    
    // Update player lists
    const team1Players = document.getElementById('team1Players');
    const team2Players = document.getElementById('team2Players');
    team1Players.innerHTML = '';
    team2Players.innerHTML = '';

    state.players.forEach(player => {
        let badges = '';
        if (player.name === state.activePlayer1 || player.name === state.activePlayer2) {
            badges += '<span class="badge bg-success">AKTİF</span> ';
        }
        if (player.name === currentPlayerName) {
            badges += '<span class="badge bg-info">SEN</span>';
        }

        const playerEl = `<li class="list-group-item d-flex justify-content-between align-items-center">${player.name} <div>${badges}</div></li>`;

        if (player.team === 'Sol') {
            team1Players.innerHTML += playerEl;
        } else if (player.team === 'Sağ') {
            team2Players.innerHTML += playerEl;
        }
    });

    // Update active players display (this might be redundant now but good for fallback)
    document.getElementById('team1Active').innerHTML = `<small>Aktif: ${state.activePlayer1 || 'N/A'}</small>`;
    document.getElementById('team2Active').innerHTML = `<small>Aktif: ${state.activePlayer2 || 'N/A'}</small>`;
    
    // Update fuse position
    const movingPoint = document.getElementById('movingPoint');
    const percentage = ((state.fusePosition + 100) / 200) * 100;
    movingPoint.style.left = percentage + '%';

    // Update pass button
    const passTurnBtn = document.getElementById('passTurnBtn');
    if (passTurnBtn) {
        const isMyTurn = state.currentTurn === currentPlayerName;
        passTurnBtn.disabled = !isMyTurn;
        passTurnBtn.innerHTML = isMyTurn 
            ? '<i class="fas fa-arrow-right"></i> Cevapla!' 
            : '<i class="fas fa-clock"></i> Sıra Rakipte';
        passTurnBtn.className = isMyTurn ? 'btn btn-lg btn-success' : 'btn btn-lg btn-secondary';
    }

    // Update timer status
     const timerStatus = document.getElementById('timerStatus');
     if(timerStatus){
        timerStatus.innerHTML = state.isTimerRunning
            ? '<span class="badge bg-warning">⏰ Süre İşliyor!</span>'
            : '<span class="badge bg-secondary">⏸️ Süre Durdu</span>';
    }
    
    // Update validation buttons for referee
    if (isReferee.toLowerCase() === 'true') {
        const validateSolBtn = document.getElementById('validate-sol');
        const validateSagBtn = document.getElementById('validate-sag');
        if (validateSolBtn && validateSagBtn) {
            validateSolBtn.disabled = state.isTeam1VolunteerAnswerValidated;
            validateSolBtn.innerHTML = '🔴 Kırmızı Takımı Doğrula';
            validateSagBtn.disabled = state.isTeam2VolunteerAnswerValidated;
            validateSagBtn.innerHTML = '🔵 Mavi Takımı Doğrula';
        }
    }
        
    // If game/round is over, stop polling and reload ONCE to show the final screen.
    if (!state.isGameActive && !roundOverReloaded) {
        if (gameUpdateInterval) clearInterval(gameUpdateInterval);
        sessionStorage.setItem('roundOverReloaded', 'true');
        setTimeout(() => location.reload(), 300);
    }

    // Soru metnini güncelle
    const questionText = document.getElementById('questionText');
    if (questionText && state.question) {
        questionText.textContent = state.question;
    }
    // Doğru cevabı güncelle (hakem için)
    const correctAnswerText = document.getElementById('correctAnswerText');
    if (correctAnswerText && state.answer) {
        correctAnswerText.textContent = state.answer;
    }
}

// Action Functions
function validateAnswer(team) {
    fetch('/Game/ValidateAnswer', {
        method: 'POST',
        headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'RequestVerificationToken': requestVerificationToken },
        body: `code=${lobbyCode}&team=${team}`
    }).catch(err => {
        console.error(err);
        alert("Doğrulama sırasında bir hata oluştu.");
    });
}

function passTurn() {
    const btn = document.getElementById('passTurnBtn');
        btn.disabled = true;
        btn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> İşleniyor...';

    fetch('/Game/PassTurn', {
        method: 'POST',
        headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'RequestVerificationToken': requestVerificationToken },
        body: `code=${lobbyCode}`
    })
    .then(res => res.json())
    .then(data => {
        if (!data.success) {
            location.reload();
        }
    }).catch(err => {
        console.error(err);
        alert("Bir ağ hatası oluştu.");
        location.reload();
    });
}

function resetGame() {
    if (confirm('Oyunu sıfırlayıp lobiye dönmek istediğinizden emin misiniz?')) {
        fetch('/Game/ResetGame', {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'RequestVerificationToken': requestVerificationToken },
            body: `code=${lobbyCode}`
        })
        .then(res => res.json())
        .then(data => {
            if (!data.success) alert(data.message || 'Bir hata oluştu.');
        }).catch(err => console.error(err));
    }
}

function volunteerForTeam(team) {
    fetch('/Game/VolunteerForTeam', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/x-www-form-urlencoded',
            'RequestVerificationToken': requestVerificationToken
        },
        body: `code=${lobbyCode}&team=${team}`
    })
    .then(res => res.json())
    .then(data => {
        if (data.success) {
            location.reload(); // veya SignalR ile otomatik güncelleniyorsa reload gerekmez
        } else {
            alert(data.message || 'Bir hata oluştu.');
        }
    })
    .catch(err => {
        alert('Bir hata oluştu.');
    });
}

// Initialization
connection.start()
    .then(() => {
        console.log("SignalR Connected to GameHub.");
        connection.invoke("AddToGroup", lobbyCode);
        if (!roundOverReloaded) {
            fetchAndUpdateUI();
            gameUpdateInterval = setInterval(fetchAndUpdateUI, 50);
        }
    })
    .catch(err => console.error("SignalR Connection Error: ", err));
    
window.addEventListener('beforeunload', () => {
    if (gameUpdateInterval) clearInterval(gameUpdateInterval);
}); 