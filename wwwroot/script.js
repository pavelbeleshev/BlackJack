console.log("✅ Скрипт загружен!");

let connection = null;
let playerName = '';
let roomId = '';

document.getElementById('createBtn').onclick = async () => {
    playerName = document.getElementById('playerName').value || 'Аноним';
    await startConnection();
    await connection.invoke('CreateRoom', playerName);
};

document.getElementById('joinBtn').onclick = async () => {
    roomId = document.getElementById('roomCode').value;
    if (!roomId) return;
    playerName = document.getElementById('playerName').value || 'Аноним';
    await startConnection();
    await connection.invoke('JoinRoom', roomId, playerName);
};

async function startConnection() {
    connection = new signalR.HubConnectionBuilder()
        .withUrl('/gameHub')
        .build();
    
    connection.on('RoomCreated', (id) => {
        roomId = id;
        addMessage(`✅ Комната создана! Код: ${id}`);
        document.getElementById('roomInfo').innerHTML = `<strong>Код комнаты: ${id}</strong><br>Отправь код другу!`;
        document.getElementById('login').classList.add('hidden');
        document.getElementById('game').classList.remove('hidden');
    });
    
    connection.on('GameReady', (data) => {
        addMessage(`🎮 Игроки: ${data.players.map(p => p.name).join(', ')}`);
    });
    
    connection.on('RoundStarted', () => {
        addMessage('🔄 Новый раунд!');
    });
    
    connection.on('Error', (msg) => {
        addMessage(`❌ ${msg}`);
    });
    
    await connection.start();
    addMessage('🔌 Подключено к серверу');
}

function addMessage(msg) {
    const div = document.getElementById('messages');
    div.innerHTML += `<div>> ${msg}</div>`;
    div.scrollTop = div.scrollHeight;
}
