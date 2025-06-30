const lobbyCode = window.lobbyCode;
const currentPlayerName = window.currentPlayerName;
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
let lastKnownTurn = null; // Keep track of whose turn it was

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

    // Update current turn indicator
    const currentTurnPlayer = document.getElementById('current-turn-player');
    if (currentTurnPlayer) {
        currentTurnPlayer.textContent = state.currentTurn;
        if (state.currentTurn === state.activePlayer1) {
            currentTurnPlayer.className = 'text-danger';
        } else {
            currentTurnPlayer.className = 'text-primary';
        }
    }

    // Update timer status
     const timerStatus = document.getElementById('timerStatus');
     if(timerStatus){
        timerStatus.innerHTML = state.isTimerRunning
            ? '<span class="badge bg-warning">⏰ Süre İşliyor!</span>'
            : '<span class="badge bg-secondary">⏸️ Süre Durdu</span>';
    }
    
    // Show/hide answer form based on turn
    const answerContainer = document.getElementById('answer-form-container');
    if (answerContainer) {
        const isMyTurn = state.currentTurn === currentPlayerName;
        answerContainer.style.display = isMyTurn ? 'block' : 'none';
        
        // Turn just changed to this player
        if (isMyTurn && lastKnownTurn !== state.currentTurn) {
            // Reset input field
            const answerInput = document.getElementById('answerInput');
            if (answerInput) answerInput.value = '';

            // Automatically start speech recognition if supported and not already running
            if (recognition && !isRecognizing) {
                toggleSpeechRecognition();
            }
        }

        // Turn just changed away from this player, so stop recognition
        if (!isMyTurn && isRecognizing) {
            toggleSpeechRecognition(); // This will call recognition.stop()
        }
    }
        
    // Update the last known turn at the end of the update
    lastKnownTurn = state.currentTurn;

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
    const correctAnswerText = document.getElementById('correctAnswerText');
    if (correctAnswerText && state.answer) {
        correctAnswerText.textContent = state.answer;
    }
}

function checkAnswer() {
    const answerInput = document.getElementById('answerInput');
    const answer = answerInput.value;

    // Sunucuya boş istek göndermemek için temel bir kontrol.
    if (!answer) {
        return;
    }

    fetch('/Game/SubmitAnswer', {
        method: 'POST',
        headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'RequestVerificationToken': requestVerificationToken },
        body: `code=${lobbyCode}&answer=${encodeURIComponent(answer)}`
    })
    .then(res => res.json())
    .then(data => {
        // Sunucudan { success: true, isCorrect: true } geldiğinde,
        // SignalR zaten arayüzü güncelleyecektir (formu gizleyecek vb.).
        // isCorrect: false ise, kullanıcı yazmaya devam edebilir, bu yüzden bir şey yapmaya gerek yok.
        if (!data.success && data.message) {
             console.error("Hata: " + data.message);
        }
    }).catch(err => {
        console.error("Cevap gönderilirken bir ağ hatası oluştu:", err);
    });
}

const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;
let recognition;
let isRecognizing = false;

if (SpeechRecognition) {
    recognition = new SpeechRecognition();
    recognition.continuous = true;
    recognition.lang = 'tr-TR';
    recognition.interimResults = false;
    recognition.maxAlternatives = 1;

    recognition.onresult = (event) => {
        let transcript = event.results[0][0].transcript;
        // Sonundaki noktayı kaldır
        if (transcript.endsWith('.')) {
            transcript = transcript.slice(0, -1);
        }
        const answerInput = document.getElementById('answerInput');
        answerInput.value = transcript;
        checkAnswer(); // Check the answer automatically
    };

    recognition.onend = () => {
        isRecognizing = false;
        const speechBtn = document.getElementById('speechBtn');
        if (speechBtn) {
            speechBtn.classList.remove('btn-danger');
            speechBtn.classList.add('btn-outline-secondary');
            speechBtn.innerHTML = '<i class="fas fa-microphone"></i>';
        }
    };

    recognition.onerror = (event) => {
        console.error('Speech recognition error:', event.error);
        isRecognizing = false;
    };
} else {
    // Hide the button if the browser doesn't support the API
    window.addEventListener('DOMContentLoaded', () => {
        const speechBtn = document.getElementById('speechBtn');
        if(speechBtn) speechBtn.style.display = 'none';
    });
}

function toggleSpeechRecognition() {
    if (!recognition) return;

    const speechBtn = document.getElementById('speechBtn');
    if (isRecognizing) {
        recognition.stop();
    } else {
        recognition.start();
        isRecognizing = true;
        if (speechBtn) {
            speechBtn.classList.remove('btn-outline-secondary');
            speechBtn.classList.add('btn-danger');
            speechBtn.innerHTML = '<i class="fas fa-microphone-slash"></i> Durdur';
        }
    }
}

// Action Functions
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