# azres
Excel dump of Azure Resources
![image.png](Screenshot.png)

#### USAGE
Save the JSON from below link and provide the file-path as input to `azres.exe`
https://resources.azure.com/subscriptions/{subscription-id}/resourceGroups/{resourceGroup-id}/resources"

##### CONTRIBUTION
```batch
# Publish package to nuget.org
nuget push ./bin/AzRes.1.0.0.nupkg -ApiKey <key> -Source https://api.nuget.org/v3/index.json

# Install from nuget.org
dotnet tool install -g azres
dotnet tool install -g azres --version 1.0.x

# Install from local project path
dotnet tool install -g --add-source ./bin azres

# Uninstall
dotnet tool uninstall -g azres
```
> **NOTE**: If the Tool is not accesible post installation, add `%USERPROFILE%\.dotnet\tools` to the PATH env-var.