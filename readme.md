### Overview

**Built against Umbraco 7.13.1 and Umbraco Forms 7.0.5**

A FileSystemProvider for Umbraco used for commiting files to Visual Studio Online Git repositories.
Wraps `PhysicalFileSystem` by default, but can be inherited to wrap others.

### Installation

Pre-release on nuget:  

    install-package our.umbraco.filesystemproviders.vso -pre

### Configuration

Add the following to ~/config/fileSystemProviders.config:

    <Provider alias="forms" type="Our.Umbraco.FileSystemProviders.Vso.VsoGitFileSystemProvider, Our.Umbraco.FileSystemProviders.Vso">
        <Parameters>
            <add key="virtualRoot" value="~/App_Data/UmbracoForms/Data/" />
            <add key="gitUrl" value="https://[youraccount].visualstudio.com"/>
            <add key="username" value="[your username]"/>
            <add key="password" value="[token or basic pass]"/>
            <add key="repositoryId" value="[repository id (guid)]"/>
            <add key="repoRoot" value="[path to website root in git]"/>
            <add key="environment" value="development"/>
        </Parameters>
    </Provider>

The environment variable defines the branch forms files will be checked in to.  
For instance forms/development, forms/staging and forms/production.  
The idea is to merge (and squash) the branches before deployment.