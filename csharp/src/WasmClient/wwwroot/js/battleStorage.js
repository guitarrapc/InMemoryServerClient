// バトル履歴管理用IndexedDB操作
window.battleStorage = {
  dbName: 'WasmBattleClientDB',
  dbVersion: 1,
  storeName: 'battleHistory',

  // データベース初期化
  async initDB() {
    return new Promise((resolve, reject) => {
      const request = indexedDB.open(this.dbName, this.dbVersion);

      request.onerror = () => {
        console.error('IndexedDB initialization failed:', request.error);
        reject(request.error);
      };

      request.onsuccess = () => {
        console.log('IndexedDB initialized successfully');
        resolve(request.result);
      };

      request.onupgradeneeded = (event) => {
        const db = event.target.result;

        // バトル履歴テーブル作成
        if (!db.objectStoreNames.contains(this.storeName)) {
          const store = db.createObjectStore(this.storeName, { keyPath: 'battleId' });

          // インデックス作成
          store.createIndex('createdAt', 'createdAt', { unique: false });
          store.createIndex('groupName', 'groupName', { unique: false });
          store.createIndex('serverUrl', 'serverUrl', { unique: false });
          store.createIndex('completedAt', 'completedAt', { unique: false });

          console.log('Battle history object store created with indexes');
        }
      };
    });
  },

  // バトル履歴保存
  async saveBattle(battleHistoryData) {
    const db = await this.initDB();
    const transaction = db.transaction([this.storeName], 'readwrite');
    const store = transaction.objectStore(this.storeName);

    // データサイズを計算
    const serializedData = JSON.stringify(battleHistoryData);
    const dataSize = new Blob([serializedData]).size;
    battleHistoryData.dataSizeBytes = dataSize;

    return new Promise((resolve, reject) => {
      const request = store.put(battleHistoryData);
      request.onsuccess = () => {
        console.log(`Battle ${battleHistoryData.battleId} saved (${(dataSize / 1024).toFixed(1)}KB)`);
        resolve();
      };
      request.onerror = () => {
        console.error('Failed to save battle:', request.error);
        reject(request.error);
      };
    });
  },

  // バトル履歴取得
  async getBattle(battleId) {
    const db = await this.initDB();
    const transaction = db.transaction([this.storeName], 'readonly');
    const store = transaction.objectStore(this.storeName);

    return new Promise((resolve, reject) => {
      const request = store.get(battleId);
      request.onsuccess = () => {
        const result = request.result || null;
        if (result) {
          console.log(`Battle ${battleId} retrieved (${(result.dataSizeBytes / 1024).toFixed(1)}KB)`);
        }
        resolve(result);
      };
      request.onerror = () => {
        console.error('Failed to retrieve battle:', request.error);
        reject(request.error);
      };
    });
  },

  // バトル履歴一覧取得（軽量なサマリー情報のみ）
  async getBattleList(limit = 50) {
    const db = await this.initDB();
    const transaction = db.transaction([this.storeName], 'readonly');
    const store = transaction.objectStore(this.storeName);
    const index = store.index('completedAt');

    return new Promise((resolve, reject) => {
      const battles = [];
      const request = index.openCursor(null, 'prev'); // 新しい順

      request.onsuccess = (event) => {
        const cursor = event.target.result;
        if (cursor && battles.length < limit) {
          const battle = cursor.value;

          // 軽量なサマリーデータのみ抽出
          battles.push({
            battleId: battle.battleId,
            createdAt: battle.createdAt,
            completedAt: battle.completedAt,
            groupName: battle.groupName,
            serverUrl: battle.serverUrl,
            totalTurns: battle.totalTurns,
            isPlayerVictory: battle.result?.isPlayerVictory || false,
            dataSizeKB: Math.round((battle.dataSizeBytes || 0) / 1024),
            clientCount: battle.participatingClients?.length || 0
          });

          cursor.continue();
        } else {
          console.log(`Retrieved ${battles.length} battle summaries`);
          resolve(battles);
        }
      };

      request.onerror = () => {
        console.error('Failed to retrieve battle list:', request.error);
        reject(request.error);
      };
    });
  },

  // バトル履歴削除
  async deleteBattle(battleId) {
    const db = await this.initDB();
    const transaction = db.transaction([this.storeName], 'readwrite');
    const store = transaction.objectStore(this.storeName);

    return new Promise((resolve, reject) => {
      const request = store.delete(battleId);
      request.onsuccess = () => {
        console.log(`Battle ${battleId} deleted`);
        resolve();
      };
      request.onerror = () => {
        console.error('Failed to delete battle:', request.error);
        reject(request.error);
      };
    });
  },

  // 全バトル履歴削除
  async clearAllBattles() {
    const db = await this.initDB();
    const transaction = db.transaction([this.storeName], 'readwrite');
    const store = transaction.objectStore(this.storeName);

    return new Promise((resolve, reject) => {
      const request = store.clear();
      request.onsuccess = () => {
        console.log('All battle history cleared');
        resolve();
      };
      request.onerror = () => {
        console.error('Failed to clear battle history:', request.error);
        reject(request.error);
      };
    });
  },

  // ストレージ統計情報取得
  async getStorageStats() {
    const db = await this.initDB();
    const transaction = db.transaction([this.storeName], 'readonly');
    const store = transaction.objectStore(this.storeName);

    return new Promise((resolve, reject) => {
      let totalSize = 0;
      let totalCount = 0;
      let oldestDate = null;
      let newestDate = null;

      const request = store.openCursor();
      request.onsuccess = (event) => {
        const cursor = event.target.result;
        if (cursor) {
          const battle = cursor.value;
          totalSize += battle.dataSizeBytes || 0;
          totalCount++;

          const completedAt = new Date(battle.completedAt);
          if (!oldestDate || completedAt < oldestDate) oldestDate = completedAt;
          if (!newestDate || completedAt > newestDate) newestDate = completedAt;

          cursor.continue();
        } else {
          resolve({
            totalBattles: totalCount,
            totalSizeBytes: totalSize,
            oldestBattle: oldestDate?.toISOString(),
            newestBattle: newestDate?.toISOString()
          });
        }
      };

      request.onerror = () => {
        console.error('Failed to calculate storage stats:', request.error);
        reject(request.error);
      };
    });
  }
};

// ページロード時に初期化
document.addEventListener('DOMContentLoaded', () => {
  window.battleStorage.initDB().catch(console.error);
});
