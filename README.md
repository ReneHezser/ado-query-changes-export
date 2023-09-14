# Azure DevOps - Export changes in query results

Keeping track of Azure DevOps items can be hard, if you need to monitor many items and they change rarely. This tool will help you by executing the query, looking for changes in the resulting items within the last x days and export the result to an HTML page.

**Disclaimer:** *this tool does not shine with great coding, as this was not the purpose. Treat it like a demo and improve if you want to use it in a production way!*

# Description

In my case Feedback items are associated to Features, which are updated by somebody else.

![Azure DevOps Dashboard](/assets/ado-query-result.png)

I wanted to see changes on the associated items (in this case the Features) with field changes within the last say 7 days.

## Changelog

### 1.0.0

- initial version

# Requirements

- Azure DevOps Personal Access Token (PAT)
- Query that returns linked items like in the screenshot above
- ```.env``` file

## PAT

A personal access token has to be generated for the organization that hosts the items to query.

![Getting to User Settings in Azure DevOps](/assets/ado-user-settings.png)

Create a new personal access token with "Work Items: Read" (copy the generated pat, as you won't be able to retrieve it later).

## ADO Query

An existing query can be exported as **wiql** query in the editor.

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

This query needs to be copied to the file ```ado-query.wiql```. It is important to replace ```@project``` with ```'{0}'```, as the program will insert the project from the env file into the query prior executing.

## .env file

This file provides environment variables. Please create the file and fill the values accordingly.

```batch
ORGANIZATION=<your organization>
PROJECT=<your project>
PERSONAL_ACCESS_TOKEN=<the pat created earlier>
```

# Run the tool

The [releases folder](/releases/) contains the application. In order to run it you need to

1. add an ```.env``` file
2. adjust the wiql query in ```ado-query.wiql```
3. execute ```.\AdoQueries.exe 7``` with an optional parameter to specify the number of days to look for changes. The default is 7.

The output html will be stored in the same folder.

# Build it

```powershell
dotnet build
dotnet publish -r win-x64
dotnet publish -r linux-x64 --self-contained false
```