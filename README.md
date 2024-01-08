# Azure DevOps - Export changes in query results

Keeping track of Azure DevOps items can be hard, if you need to monitor many items and they change rarely. This tool will help you by executing the query, looking for changes in the resulting items within the last x days and export the result to an HTML page.

**Disclaimer:** *this tool does not shine with great coding, as this was not in scope. Treat it like a demo and improve if you want to use it in a production way!*

# Description

In my case Feedback items are associated to Features, which are updated by somebody else.

![Azure DevOps Dashboard](/assets/ado-query-result.png)

I wanted to see changes on the associated items (in this case the Features) with field changes within the last say 7 days.

## Changelog

### 1.0.10

- Modified Events for Application Insights

### 1.0.9

- refactoring plugin loading for Docker usage

### 1.0.8

- added the option to exclude item versions that have been modified by a specific user

### 1.0.7

- added the option to implement a custom "ignore certain changes" like "don't overwrite values with an empty value".

### 1.0.6

- fixed old value being empty

### 1.0.5

- fixed path for Linux to load Plugins

### 1.0.4

- implemented batching to fix the 200 WorkItem limit

### 1.0.3

- improvements for logging
- exception handling improved

### 1.0.2

- added Application Insights
- renamed ICommand to IPlugin

### 1.0.1

- implemented plugin functionality
- extensions for PluginBase to be used in custom Plugins
- Interfaces for objects

### 1.0.0

- initial version

# Requirements

- Azure DevOps Personal Access Token (PAT)
- Query that returns linked items like in the screenshot above
- ```.env``` file
- ```appsettings.json``` file

## TODO

- ...

## PAT

A personal access token has to be generated for the organization that hosts the items to query.

![Getting to User Settings in Azure DevOps](/assets/ado-user-settings.png)

Create a new personal access token with "Work Items: Read" (copy the generated pat, as you won't be able to retrieve it later).

## ADO Query

An existing query can be exported as **wiql** query in the editor, after you install this extension to Azure DevOps [Wiql Editor](https://marketplace.visualstudio.com/items?itemName=ottostreifel.wiql-editor).

![Export wiql query from editor](/assets/ado-query-export.png)

*This tool will only look at changes in items that have a parent. In the first screenshot you can see that Features have a parent (Feedbacks).*

```
SELECT
    [System.Id],
    [System.WorkItemType],
    [System.Title],
    [System.State]
FROM workitemLinks
WHERE
    (
        [Source].[System.TeamProject] = '{0}'
        AND [Source].[System.WorkItemType] = 'Feedback'
        AND [Source].[System.State] <> ''
    )
    AND (
        [System.Links.LinkType] = 'System.LinkTypes.Hierarchy-Reverse'
    )
    AND (
        [Target].[System.TeamProject] = '{0}'
        AND [Target].[System.WorkItemType] = 'Feature'
    )
ORDER BY [ID]
MODE (MustContain)
```

This query needs to be copied to the ```ado-query.wiql``` file. It is important to replace ```@project``` with ```'{0}'```, as the program will replace the project from the env file into the query prior executing.

## .env file

This file provides environment variables. Please create the file and fill the values accordingly.

```batch
ORGANIZATION=<your organization>
PROJECT=<your project>
QUERY_DAYS=7
PERSONAL_ACCESS_TOKEN=<the pat created earlier>
```

The organization is the first part in the URL after dev.azure.com. Project follows in the URL after the organization.

With QUERY_DAYS you can configure how many days shall be taken into account when determining changes to an item.

## appsettings.json file

Application-specific configuration is done in the appsettings file. Create it in the same folder as the application.

```json
{
   "Logging": {
      "LogLevel": {
         "Default": "Information",
         "Microsoft": "Warning",
         "Microsoft.Hosting.Lifetime": "Information"
      }
   },
   "ApplicationInsights": {
      "ConnectionString" : "InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=...;LiveEndpoint=..."
   }
}
```

Fill in the connection string to Application Insights in the application.json.

## Run the tool

The [releases folder](/releases/) might contain the application. In order to run it you need to (everything in the same folder)

1. add an ```.env``` file
2. add an ```appsettings.json``` file
2. adjust the wiql query in ```ado-query.wiql```
3. execute ```.\AdoQueries.exe```

The output html will be stored in the same folder.

# Build it

To build the application open the workspace file in the root directory ```ChangeQueryExport.code-workspace```. Then, in the console of VSCode, build everything.

```powershell
cd HtmlExportPlugin
dotnet build
```

The plugin is built and copied to the Plugins folder.

```powershell
cd ..\ChangeQueryExport\
dotnet publish --self-contained true
```

After publishing the application to the ```bin\Debug\net7.0\win-x64\publish``` folder, the Plugin itself needs to be copied from the ```ChangeQueryExport\Plugin``` to the Plugin folder underneath the publish folder.

## Plugins

Each plugin needs to be built and the output copied to the Plugins folder. An ILogger will be passed on to the Plugin and can be used for logging to Application Insights or the console. It is configured for the application in the ```appsettings.json``` file.

# Links

- [AppWithPlugin Sample Code](https://github.com/dotnet/samples/tree/main/core/extensions/AppWithPlugin)