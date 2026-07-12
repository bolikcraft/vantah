namespace Vantah.Core.Models;

public readonly record struct TrafficSample(long RxBytes, long TxBytes, double RxBytesPerSec, double TxBytesPerSec);
