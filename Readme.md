# Play.Inventory.Contracts
Inventory libraries used by Play Economy services

## Create and publish package
```powershell
$version="1.0.3"
$owner="dotnetMicroservicesCourseASGX"
$gh_pat="[PAT HERE]"

dotnet pack src\Play.Inventory.Contracts\ --configuration Release -p:PackageVersion=$version -p:RepositoryUrl=https://github.com/$owner/Play.Inventory -o ..\packages
dotnet nuget push ..\packages\Play.Inventory.Contracts.$version.nupkg --api-key $gh_pat --source "github"
```

## Build the docker image
```powershell
$env:GH_OWNER="dotnetMicroservicesCourseASGX"
$env:GH_PAT="[PAT HERE]"
$appname="playeconomyaxsg1"
docker build --secret id=GH_OWNER --secret id=GH_PAT -t "$appname.azurecr.io/play.inventory:$version" .
```

## Run the docker image
```powershell
$cosmosDbConnString="[CONN STRING HERE]"
$serviceBusConnString="[CONN STRING HERE]"

docker run -it --rm -p 5004:5004 --name inventory -e MongoDbSettings__ConnectionString=$cosmosDbConnString -e ServiceBusSettings__ConnectionString=$serviceBusConnString -e ServiceSettings__MessageBroker="SERVICEBUS" play.inventory:$version
```
## Publish the docker image
```powershell
az acr login --name $appname
docker push "$appname.azurecr.io/play.inventory:$version"
```

## Creating the Azure Managed Identity and grantinng access to key vault secrets
```powershell 
$namespace="inventory"
az identity create --resource-group $appname --name $namespace

$IDENTITY_CLIENT_ID=az identity show -g $appname -n $namespace --query clientId -otsv
az keyvault set-policy -n $appname --secret-permissions get list --spn $IDENTITY_CLIENT_ID

``` 

## Establish the federeated identity credential
```powershell
$AKS_OIDC_ISSUER=az aks show -g $appname -n $appname --query "oidcIssuerProfile.issuerUrl" -otsv

az identity federated-credential create --name $namespace --identity-name $namespace --resource-group $appname --issuer $AKS_OIDC_ISSUER --subject "system:serviceaccount:${namespace}:${namespace}-serviceaccount" 
```
