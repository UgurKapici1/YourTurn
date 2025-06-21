document.addEventListener('DOMContentLoaded', () => {
    const lobbyDataElement = document.getElementById('lobby-data');
    if (!lobbyDataElement) {
        console.error('Lobby data element not found!');
        return;
    }

    const lobbyCode = lobbyDataElement.dataset.lobbyCode;
    const currentPlayerName = lobbyDataElement.dataset.currentPlayerName;
    const hostPlayerName = lobbyDataElement.dataset.hostPlayerName;
    const requestVerificationToken = document.querySelector('input[name="__RequestVerificationToken"]').value;

    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/LobbyHub")
        .build();

    connection.start().then(() => {
        connection.invoke("AddToGroup", lobbyCode);
        connection.invoke("RegisterPlayer", currentPlayerName);
    }).catch(err => console.error('SignalR Connection Error: ', err));

    connection.on("UpdateLobby", function () {
        location.reload();
    });

    connection.on("PeerHostRegistered", function (hostIP, hostPort) {
        console.log(`Peer host registered: ${hostIP}:${hostPort}`);
        updatePeerHostingUI(hostIP, hostPort, true);
    });

    connection.on("PeerHostOffline", function () {
        console.log("Peer host went offline");
        updatePeerHostingUI(null, null, false);
    });

    connection.on("LobbyClosed", function (message) {
        console.log("Lobby closed:", message);
        alert(message);
        window.location.href = "/Home/JoinLobby";
    });

    window.volunteerForTeam = function(team) {
        fetch('/Lobby/VolunteerForTeam', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': requestVerificationToken
            },
            body: JSON.stringify({
                code: lobbyCode,
                team: team
            })
        })
        .then(response => {
            if (!response.ok) throw new Error('Network response was not ok');
            return response.json();
        })
        .then(data => {
            if (!data.success) alert(data.message || 'Bir hata oluştu');
        })
        .catch(error => {
            console.error('Gönüllü olma hatası:', error);
            alert('Bir hata oluştu');
        });
    }

    window.withdrawVolunteer = function(team) {
        fetch('/Lobby/WithdrawVolunteer', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': requestVerificationToken
            },
            body: JSON.stringify({
                code: lobbyCode,
                team: team
            })
        })
        .then(response => {
            if (!response.ok) throw new Error('Network response was not ok');
            return response.json();
        })
        .then(data => {
            if (!data.success) alert(data.message || 'Bir hata oluştu');
        })
        .catch(error => {
            console.error('Gönüllülükten çıkma hatası:', error);
            alert('Bir hata oluştu');
        });
    }

    function updatePeerHostingUI(hostIP, hostPort, isOnline) {
        const peerHostingInfo = document.getElementById('peer-hosting-info');
        if (peerHostingInfo) {
            if (isOnline && hostIP && hostPort) {
                peerHostingInfo.innerHTML = `
                    <div class="alert alert-success">
                        <strong><i class="fas fa-server"></i> Peer Hosting Aktif</strong><br>
                        Host: ${hostPlayerName}<br>
                        IP: ${hostIP}<br>
                        Port: ${hostPort}
                    </div>
                `;
            } else {
                peerHostingInfo.innerHTML = `
                    <div class="alert alert-warning">
                        <strong><i class="fas fa-exclamation-triangle"></i> Peer Host Çevrimdışı</strong><br>
                        Host bağlantısı kesildi. Oyun devam edemez.
                    </div>
                `;
            }
        }
    }

    connection.on("GameStarted", function () {
        window.location.href = `/Game/Game?code=${lobbyCode}`;
    });

    window.copyLobbyCode = function() {
        const lobbyCodeElement = document.getElementById('lobbyCode');
        const codeText = lobbyCodeElement.textContent;
        
        if (navigator.clipboard && window.isSecureContext) {
            navigator.clipboard.writeText(codeText).then(() => {
                showCopyFeedback();
            }).catch(() => {
                fallbackCopy();
            });
        } else {
            fallbackCopy();
        }
    }
    
    function fallbackCopy() {
        const lobbyCodeElement = document.getElementById('lobbyCode');
        const tempInput = document.createElement('input');
        tempInput.value = lobbyCodeElement.textContent;
        tempInput.style.position = 'absolute';
        tempInput.style.left = '-9999px';
        document.body.appendChild(tempInput);
        tempInput.select();
        tempInput.setSelectionRange(0, 99999);
        document.execCommand('copy');
        document.body.removeChild(tempInput);
        showCopyFeedback();
    }
    
    function showCopyFeedback() {
        const lobbyCodeElement = document.getElementById('lobbyCode');
        const originalText = lobbyCodeElement.textContent;
        const originalClass = lobbyCodeElement.className;
        
        lobbyCodeElement.textContent = '✓ Kopyalandı!';
        lobbyCodeElement.className = 'text-success';
        lobbyCodeElement.style.fontWeight = 'bold';
        
        setTimeout(() => {
            lobbyCodeElement.textContent = originalText;
            lobbyCodeElement.className = originalClass;
            lobbyCodeElement.style.fontWeight = 'normal';
        }, 2000);
    }
}); 