FbTracker
============

FbTracker is a free Futbin price tracker (Snipe tracker) for trading in FUT Market. For every tradable card via Telegram.Bot sending notifications to chat.

![Example](https://github.com/vasjen/fbtracker/blob/master/img/Example.png)

## Requirements
- .NET 7.0 or higher
- MSSQL 2019 or higher

## Get started
1. Add your Proxy to appsetings.json
2. Enable secret storage
```powershell
dotnet user-secrets init
```
3. Add your secrets to storage (MSSQL root password, Proxy login and password, TelegramBot API
```powershell
dotnet user-secrets set "Telegram:Token" "<Your API>"
dotnet user-secrets set "Proxy:Login" "<Your Proxy Login>"
dotnet user-secrets set "Proxy:Password" "<Your Proxy Password>"
dotnet user-secrets set "DbPassword" "<Your DB Password>"
```
4. Run application using CLI
```powershell
dotnet run
```
or build and run from Visual Studio
>Note: Seeding data from the first running can take a time
## Examples
See the [Telegram Price Cheker](https://t.me/futpricecheker)  for example
