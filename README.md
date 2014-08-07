WebStreams
===============
<p align="center">
  <img src="https://github.com/daprlabs/WebStreamServer/blob/master/logo_icon.png" />
</p>

Serve APIs returning &amp; consuming observable streams over WebSockets using .NET

Think of it as ASP.NET Web API but with controllers which return `IObservable<T>` instead of `Task<T>`, which allows you to easily push to clients.

### Why not SignalR?
SignalR is great for a lot of realtime applications which scaleout easily, but it's not easy to create realtime apps which require authoritative logic - take a game for example, or other complex apps. This is perhaps because SignalR gives you transparent scale-out, so the rendezvous point for notifications (Service Bus, SQL Server, etc) does not contain application logic.

Instead, WebStreams does not give you transparent scale-out at all. Scale-out can easily be achieved using Service Bus or another backplane like in a SignalR app, but often scale-out will sit in Microsoft Orleans or another Actor framework.
Another option for scale-out is Azure Event Hubs, which can contain application logic.

See the [WebStreamSamples](https://github.com/daprlabs/WebStreamSamples) repository for a sample server with a Web client.

TODO: publish sample which scales out app logic using Microsoft Orleans.

## Installation
```
PM> Install-Package Dapr.WebStream.Server
```
NuGet will pull in all dependencies (Owin, Json.NET, Rx).

## Usage
Call UseWebStream() in your OWIN startup method. See below for an example.

StockController.cs
```
[RoutePrefix("/stock")]
public class StockTickerController
{
  private readonly ConcurrentDictionary<string, IObservable<Stock>> stocks =
    new ConcurrentDictionary<string, IObservable<Stock>>();

  [Route("ticker")]
  public IObservable<Stock> GetTicker(string symbol)
  {
    return this.GetStockTicker(symbol);
  }
  private static IObservable<Stock> CreateStockTicker(string symbol)
  {
    var random = new Random();
    // Construct a shoddy, pseudo-random walk.
    return
      Observable.Interval(TimeSpan.FromSeconds(0.1))
        .Select(time => random.NextDouble())
        .Scan((double)random.Next(1000), (prev, randomVal) =>
          Math.Max(0, prev * (1 + (0.01 * (randomVal - 0.5)))))
        .Select(val => new Stock(symbol, val, DateTime.UtcNow));
  }
  private IObservable<Stock> GetStockTicker(string symbol)
  {
    return this.stocks.GetOrAdd(symbol, CreateStockTicker);
  }
}
```

In your OWIN Startup.cs
```
public void Configuration(IAppBuilder app)
{
    app.UseWebStream();
}
```
