using System.Threading.Tasks;

namespace PricePulse.Core.Interfaces;

public interface IServer
{
    string BaseAddress { get; }
    Task StopAsync();
}