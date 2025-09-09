using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using WasmClient.Models;

namespace WasmClient.Services
{
    public class RealtimeUpdateService
    {
        private readonly HubConnection _hubConnection;
        private readonly List<Action<BattleFieldData>> _subscribers = new();

        public RealtimeUpdateService(string serverUrl)
        {
            _hubConnection = new HubConnectionBuilder()
                .WithUrl(serverUrl)
                .Build();
        }

        public async Task StartAsync()
        {
            await _hubConnection.StartAsync();
            _hubConnection.On<BattleFieldData>("ReceiveBattleFieldUpdate", NotifySubscribers);
        }

        public async Task StopAsync()
        {
            await _hubConnection.StopAsync();
        }

        public void Subscribe(Action<BattleFieldData> subscriber)
        {
            _subscribers.Add(subscriber);
        }

        public void Unsubscribe(Action<BattleFieldData> subscriber)
        {
            _subscribers.Remove(subscriber);
        }

        private void NotifySubscribers(BattleFieldData battleFieldData)
        {
            foreach (var subscriber in _subscribers)
            {
                subscriber.Invoke(battleFieldData);
            }
        }
    }
}