using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WasmClient.Models;

namespace WasmClient.Services
{
    public class FieldVisualizationService
    {
        private readonly ILogger<FieldVisualizationService> _logger;
        private readonly List<EntityData> _entities;

        public FieldVisualizationService(ILogger<FieldVisualizationService> logger)
        {
            _logger = logger;
            _entities = new List<EntityData>();
        }

        public void UpdateEntities(IEnumerable<EntityData> updatedEntities)
        {
            _entities.Clear();
            _entities.AddRange(updatedEntities);
            _logger.LogInformation("Entities updated: {Count}", _entities.Count);
        }

        public IReadOnlyList<EntityData> GetEntities() => _entities.AsReadOnly();

        public void AddEntity(EntityData entity)
        {
            _entities.Add(entity);
            _logger.LogInformation("Entity added: {EntityId}", entity.Id);
        }

        public void RemoveEntity(string entityId)
        {
            var entity = _entities.FirstOrDefault(e => e.Id == entityId);
            if (entity != null)
            {
                _entities.Remove(entity);
                _logger.LogInformation("Entity removed: {EntityId}", entityId);
            }
        }
    }
}