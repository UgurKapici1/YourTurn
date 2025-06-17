// Peer-to-Peer Hosting System
class PeerHostingSystem {
    constructor() {
        this.connection = null;
        this.lobbyCode = null;
        this.isHost = false;
        this.heartbeatInterval = null;
        this.hostIP = null;
        this.hostPort = null;
        this.peerConnections = new Map();
    }

    // Initialize SignalR connection
    async initializeConnection(lobbyCode) {
        this.lobbyCode = lobbyCode;
        
        // Get current player name from session
        const playerName = this.getPlayerNameFromSession();
        
        // Check if current player is host
        this.isHost = await this.checkIfHost(lobbyCode, playerName);
        
        // Initialize SignalR connection
        this.connection = new signalR.HubConnectionBuilder()
            .withUrl("/lobbyHub")
            .build();

        // Register event handlers
        this.registerEventHandlers();
        
        // Start connection
        await this.connection.start();
        await this.connection.invoke("AddToGroup", lobbyCode);
        
        // Register player connection
        if (playerName) {
            await this.connection.invoke("RegisterPlayer", playerName);
        }
        
        // If this player is host, start peer hosting automatically
        if (this.isHost) {
            await this.startPeerHosting();
        }
    }

    // Register SignalR event handlers
    registerEventHandlers() {
        this.connection.on("PeerHostRegistered", (hostIP, hostPort) => {
            console.log(`Peer host registered: ${hostIP}:${hostPort}`);
            this.hostIP = hostIP;
            this.hostPort = hostPort;
            this.updateUIForPeerHosting();
        });

        this.connection.on("PeerHostOffline", () => {
            console.log("Peer host went offline");
            this.hostIP = null;
            this.hostPort = null;
            this.updateUIForHostOffline();
        });

        this.connection.on("LobbyClosed", (message) => {
            console.log("Lobby closed:", message);
            this.stopPeerHosting();
            alert(message);
            window.location.href = "/Home/JoinLobby";
        });

        this.connection.on("UpdateLobby", () => {
            // Refresh lobby page
            location.reload();
        });
    }

    // Start peer hosting as host (now automatic)
    async startPeerHosting() {
        try {
            // Get local IP address (this would need to be implemented based on your requirements)
            const localIP = await this.getLocalIPAddress();
            const localPort = await this.getAvailablePort();
            
            // Register as peer host
            await this.connection.invoke("RegisterPeerHost", this.lobbyCode, localIP, localPort);
            
            // Start heartbeat
            this.startHeartbeat();
            
            // Start local server for peer connections
            await this.startLocalServer(localIP, localPort);
            
            console.log(`Peer hosting started automatically on ${localIP}:${localPort}`);
        } catch (error) {
            console.error("Failed to start peer hosting:", error);
        }
    }

    // Stop peer hosting
    stopPeerHosting() {
        if (this.heartbeatInterval) {
            clearInterval(this.heartbeatInterval);
            this.heartbeatInterval = null;
        }
        
        // Close peer connections
        this.peerConnections.forEach(connection => {
            connection.close();
        });
        this.peerConnections.clear();
        
        console.log("Peer hosting stopped");
    }

    // Start heartbeat to keep host status alive
    startHeartbeat() {
        this.heartbeatInterval = setInterval(async () => {
            try {
                await this.connection.invoke("HostHeartbeat", this.lobbyCode);
            } catch (error) {
                console.error("Heartbeat failed:", error);
            }
        }, 10000); // Send heartbeat every 10 seconds
    }

    // Get local IP address (simplified implementation)
    async getLocalIPAddress() {
        // In a real implementation, you would use WebRTC or similar to get local IP
        // For now, we'll use a placeholder
        return "127.0.0.1";
    }

    // Get available port (simplified implementation)
    async getAvailablePort() {
        // In a real implementation, you would scan for available ports
        // For now, we'll use a random port in a safe range
        return Math.floor(Math.random() * 10000) + 8000;
    }

    // Start local server for peer connections
    async startLocalServer(ip, port) {
        // This would typically involve WebRTC or WebSocket server
        // For now, we'll just log the information
        console.log(`Local server would start on ${ip}:${port}`);
    }

    // Check if current player is host
    async checkIfHost(lobbyCode, playerName) {
        try {
            const response = await fetch(`/Lobby/GetPeerHostInfo?code=${lobbyCode}`);
            if (response.ok) {
                const data = await response.json();
                return data.isHost === true;
            }
        } catch (error) {
            console.error("Failed to check host status:", error);
        }
        return false;
    }

    // Get player name from session
    getPlayerNameFromSession() {
        // This would need to be implemented based on how you store session data
        // For now, we'll try to get it from a hidden input or data attribute
        const playerNameElement = document.querySelector('[data-player-name]');
        return playerNameElement ? playerNameElement.dataset.playerName : null;
    }

    // Update UI for peer hosting
    updateUIForPeerHosting() {
        const peerHostingInfo = document.getElementById('peer-hosting-info');
        if (peerHostingInfo) {
            peerHostingInfo.innerHTML = `
                <div class="alert alert-success">
                    <strong>Peer Hosting Aktif</strong><br>
                    Host IP: ${this.hostIP}<br>
                    Port: ${this.hostPort}
                </div>
            `;
        }
    }

    // Update UI for host offline
    updateUIForHostOffline() {
        const peerHostingInfo = document.getElementById('peer-hosting-info');
        if (peerHostingInfo) {
            peerHostingInfo.innerHTML = `
                <div class="alert alert-warning">
                    <strong>Peer Host Çevrimdışı</strong><br>
                    Host bağlantısı kesildi. Oyun devam edemez.
                </div>
            `;
        }
    }

    // Connect to peer host
    async connectToPeerHost() {
        if (!this.hostIP || !this.hostPort) {
            console.error("No peer host information available");
            return;
        }

        try {
            // This would involve WebRTC or WebSocket connection to peer host
            console.log(`Connecting to peer host at ${this.hostIP}:${this.hostPort}`);
            
            // For now, we'll just log the connection attempt
            // In a real implementation, you would establish the actual connection
        } catch (error) {
            console.error("Failed to connect to peer host:", error);
        }
    }

    // Cleanup on page unload
    cleanup() {
        this.stopPeerHosting();
        if (this.connection) {
            this.connection.stop();
        }
    }
}

// Initialize peer hosting system when page loads
document.addEventListener('DOMContentLoaded', function() {
    const lobbyCode = document.querySelector('[data-lobby-code]')?.dataset.lobbyCode;
    
    if (lobbyCode) {
        window.peerHostingSystem = new PeerHostingSystem();
        window.peerHostingSystem.initializeConnection(lobbyCode);
        
        // Cleanup on page unload
        window.addEventListener('beforeunload', () => {
            window.peerHostingSystem.cleanup();
        });
    }
});

// Export for use in other scripts
window.PeerHostingSystem = PeerHostingSystem; 