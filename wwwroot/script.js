console.log("✅ Скрипт загружен!");

let connection = null;
let playerName = '';
let roomId = '';

const loginDiv = document.getElementById('login');
const gameDiv = document.getElementById('game');
const roomInfoDiv = document.getElementById('roomInfo');
const dealerCardsDiv = document.getElementById('dealerCards');
const dealerPointsSpan = document.getElementById('dealerPoints');
const playerCardsDiv = document.getElementById('playerCards');
const playerPointsSpan = document.getElementById('playerPoints');
const balanceDiv = document.getElementById('balance');
const bettingArea = document.getElementById('bettingArea');
const actionsDiv = document.getElementById('actions');
const messagesDiv = document.getElementById('messages');

document.getElementById('createBtn').onclick = async () => {
    console.log("Создание комнаты");
    playerName = document.getElementById('playerName').value || 'Аноним';
    await startConnection();
    await connection.invoke('CreateRoom', playerName);
};

document.getElementById('joinBtn').onclick = async () => {
    console.log("Присоединение к комнате");
    roomId = document.getElementById('roomCode').value;
    if (!roomId) {
        addMessage('❌ Введите код комнаты');
        return;
    }
    playerName = document.getElementById('playerName').value || 'Аноним';
    await startConnection();
    await connection.invoke('JoinRoom', roomId, playerName);
};

document.getElementById('betBtn').onclick = async () => {
    const amount = parseInt(document.getElementById('betAmount').value);
    if (isNaN(amount) || amount <= 0) {
        addMessage('❌ Введите корректную ставку');
        return;
    }
    console.log("Ставка:", amount);
    await connection.invoke('MakeBet', amount);
};

document.getElementById('hitBtn').onclick = async () => {
    await connection.invoke('Hit');
};

document.getElementById('standBtn').onclick = async () => {
    await connection.invoke('Stand');
};

async function startConnection() {
    connection = new signalR.HubConnectionBuilder()
        .withUrl('/gameHub')
        .build();
    
    connection.on('RoomCreated', (id) => {
        console.log("RoomCreated:", id);
        roomId = id;
        addMessage(`✅ Комната создана! Код: ${id}`);
        roomInfoDiv.innerHTML = `<strong>📌 КОД КОМНАТЫ: ${id}</strong><br>Отправь этот код другу!`;
        loginDiv.classList.add('hidden');
        gameDiv.classList.remove('hidden');
    });
    
    connection.on('GameReady', (data) => {
        console.log("GameReady:", data);
        addMessage(`🎮 Игроки: ${data.players.map(p => p.name).join(', ')}`);
        roomInfoDiv.innerHTML = `<strong>📌 КОМНАТА: ${data.roomId}</strong><br>Игроки: ${data.players.map(p => `${p.name} (${p.balance}💰)`).join(', ')}`;
        loginDiv.classList.add('hidden');
        gameDiv.classList.remove('hidden');
    });
    
    connection.on('RoundStarted', () => {
        console.log("RoundStarted");
        addMessage('🔄 НОВЫЙ РАУНД! Сделайте ставку');
        bettingArea.classList.remove('hidden');
        actionsDiv.classList.add('hidden');
        clearCards();
    });
    
    connection.on('BetPlaced', (playerName, amount, newBalance) => {
        console.log("BetPlaced:", playerName, amount);
        addMessage(`💰 ${playerName} сделал ставку ${amount} фишек`);
        if (newBalance !== undefined && balanceDiv) {
            balanceDiv.innerHTML = `💰 БАЛАНС: ${newBalance} фишек`;
        }
    });
    
    connection.on('BetConfirmed', (amount) => {
        console.log("BetConfirmed:", amount);
        addMessage(`✅ Ставка ${amount} принята`);
        bettingArea.classList.add('hidden');
        actionsDiv.classList.remove('hidden');
    });
    
    connection.on('GameDealt', (data) => {
        console.log("GameDealt:", data);
        addMessage(`🃟 РАЗДАЧА! У вас ${data.playerPoints} очков`);
        updatePlayerCards(data.playerHand);
        playerPointsSpan.innerHTML = `Очки: ${data.playerPoints}`;
        
        if (data.dealerFirstCard && dealerCardsDiv) {
            dealerCardsDiv.innerHTML = `<div class="card">${data.dealerFirstCard}</div><div class="card">🃟</div>`;
        }
        
        if (balanceDiv) balanceDiv.innerHTML = `💰 БАЛАНС: ${data.balance} фишек`;
        
        if (data.hasBlackjack) addMessage(`🎉 BLACKJACK!`);
    });
    
    connection.on('UpdateYourHand', (data) => {
        updatePlayerCards(data.hand);
        playerPointsSpan.innerHTML = `Очки: ${data.points}`;
    });
    
    connection.on('PlayerHit', (name, card, points) => {
        addMessage(`🎴 ${name} взял карту ${card} (${points} очков)`);
    });
    
    connection.on('YourTurn', () => {
        addMessage(`🎯 ВАШ ХОД!`);
        actionsDiv.classList.remove('hidden');
    });
    
    connection.on('CurrentTurn', (playerId) => {
        if (playerId === connection.connectionId) {
            addMessage(`🎯 ВАШ ХОД!`);
            actionsDiv.classList.remove('hidden');
        } else {
            addMessage(`⏳ Ход другого игрока...`);
            actionsDiv.classList.add('hidden');
        }
    });
    
    connection.on('DealerReveal', (data) => {
        updateDealerCards(data.hand);
        dealerPointsSpan.innerHTML = `Очки: ${data.points}`;
        addMessage(`🤵 У дилера ${data.points} очков`);
    });
    
    connection.on('YourResult', (data) => {
        addMessage(`${data.message}`);
        if (balanceDiv) balanceDiv.innerHTML = `💰 БАЛАНС: ${data.newBalance} фишек`;
    });
    
    connection.on('RoundResults', (results) => {
        addMessage(`📊 РЕЗУЛЬТАТЫ РАУНДА:`);
        results.forEach(r => addMessage(`  ${r.name}: ${r.message} (+${r.payout}💰)`));
    });
    
    connection.on('PlayerBusted', (name) => {
        addMessage(`💀 ${name} ПЕРЕБРАЛ!`);
    });
    
    connection.on('Error', (msg) => {
        console.error("Server error:", msg);
        addMessage(`❌ ${msg}`);
    });
    
    await connection.start();
    console.log("Connected, connectionId:", connection.connectionId);
    addMessage('🔌 Подключено к серверу');
}

function updatePlayerCards(cards) {
    if (!playerCardsDiv) return;
    playerCardsDiv.innerHTML = '';
    cards.forEach(card => {
        playerCardsDiv.innerHTML += `<div class="card">${card}</div>`;
    });
}

function updateDealerCards(cards) {
    if (!dealerCardsDiv) return;
    dealerCardsDiv.innerHTML = '';
    cards.forEach(card => {
        dealerCardsDiv.innerHTML += `<div class="card">${card}</div>`;
    });
}

function clearCards() {
    if (dealerCardsDiv) dealerCardsDiv.innerHTML = '';
    if (playerCardsDiv) playerCardsDiv.innerHTML = '';
    if (dealerPointsSpan) dealerPointsSpan.innerHTML = '';
    if (playerPointsSpan) playerPointsSpan.innerHTML = '';
}

function addMessage(msg) {
    console.log(msg);
    if (messagesDiv) {
        messagesDiv.innerHTML += `<div>> ${msg}</div>`;
        messagesDiv.scrollTop = messagesDiv.scrollHeight;
    }
}