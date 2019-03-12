# ArchiveCE

An azure function that will archive app insights storage

You will need a local settings.json with storage connection strings as follows:

"DefaultEndpointsProtocol=https;AccountName=nicksfunctiondemostrg;AccountKey=[redacted];EndpointSuffix=core.windows.net",

{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "nicksfunctiondemostrg",
    "AzureWebJobsDashboard": "nicksfunctiondemostrg",
    "ArchiveStorageConnectionString": "cearchive",
    "ContinuousExportStorageConnectionString": "appinsightsexportxxx",
    "ArchiveContainerName": "archive"    
  }
}

AzureWebjobsStorage and AzureWebJobsDashboard are technical lumbing in support of the function as required by all functions and webjobs. 

ArchiveStorageConnectionString is the connection string for where you want to the data to be archived to. (I have this as a blob only storage service - which supports archive)
ArchiveContainerName is the contaier within the Archive Storage account where the archives are stored.

ContinuousExportStorageConnectionString is the connection string for the storage where App Insights has been configured to export to.

**You will need to set up and event grid trigger to pick up changes from the ArchiveStorageConnectionString stroage account and push to the webhook for the Azure Function that does the archiving**

**Give me a shout if you help setting any of this up**
