using EventManger.AlarmLogic;
using EventManger.Enums;
using SensorServerApi;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unity;

namespace EventManger.Services
{
    public class MainService
    {
        private ISensorServer _sensorServer;
        ConcurrentDictionary<Guid, SensorStatus> _statuses;
        public Subject<(OperationType operationType, SensorStatus sensorStatus, Sensor sensor)> OnSensorStatusUpdate;
        private readonly BlockingCollection<SensorStatus> _sensorStatusQueue = new BlockingCollection<SensorStatus>();


        [InjectionMethod]
        public void Inject(ISensorServer sensorServer)
        {
            _sensorServer = sensorServer;
        }

        public MainService()
        {
            _statuses = new ConcurrentDictionary<Guid, SensorStatus>();
            OnSensorStatusUpdate = new Subject<(OperationType operationType, SensorStatus sensorStatus, Sensor sensor)>();
        }

        public async Task Start()
        {
            _sensorServer.OnSensorStatusEvent += _sensorServer_OnSensorStatusEvent;
            _ = Task.Run(async () =>
            {
                foreach (var sensorStatus in _sensorStatusQueue.GetConsumingEnumerable())
                {
                    await HandleSensorStatus(sensorStatus);
                }
            });
            await _sensorServer.StartServer(Rate.Easy, isContinuous: true);

        }

        private async Task HandleSensorStatus(SensorStatus sensorStatus)
        {
            Sensor sensor = await _sensorServer.GetSensorById(sensorStatus.SensorId);

            var operationType = _statuses.ContainsKey(sensorStatus.SensorId)
                ? OperationType.Update
                : OperationType.Add;

            _statuses[sensorStatus.SensorId] = sensorStatus;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] added {sensorStatus.SensorId}, {sensor.Name} to dictionary");
            OnSensorStatusUpdate.OnNext((operationType, sensorStatus, sensor));

            _ = Task.Delay(TimeSpan.FromSeconds(15)).ContinueWith(_ =>
            {
                if (_statuses.TryGetValue(sensorStatus.SensorId, out var currentStatus) &&
                    currentStatus.TimeStamp == sensorStatus.TimeStamp)
                {
                    if (_statuses.TryRemove(sensorStatus.SensorId, out var removedStatus))
                    {
                        OnSensorStatusUpdate.OnNext((OperationType.Remove, removedStatus, null));
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] removed {removedStatus.SensorId}, {sensor.Name} from dictionary");
                    }
                }
            });
        }

        public Task DeleteStatus(Guid sensorId)
        {
            return Task.Run(() =>
            {
                if (_statuses.TryRemove(sensorId, out var sensorStatus))
                {
                    OnSensorStatusUpdate.OnNext((OperationType.Remove, sensorStatus, null));
                }
            });
        }

        public async Task Stop()
        {
            // Add code to stop the service .. 
            await _sensorServer.StopServer();
            _sensorStatusQueue.CompleteAdding();

            foreach (var item in _statuses)
            {
                OnSensorStatusUpdate.OnNext((OperationType.Remove, item.Value, null));
            }

            _statuses.Clear();
            Console.WriteLine("Service stopped");
        }

        private void _sensorServer_OnSensorStatusEvent(SensorStatus sensorStatus)
        {
            Console.WriteLine($"got {sensorStatus.SensorId}");
            _sensorStatusQueue.Add(sensorStatus);
        }
    }
}
