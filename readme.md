### Overview

**NOTE: Usage with Umbraco Forms only with nightlies for now**

A FileSystemProvider for Umbraco used for commiting files to Visual Studio Online Git repositories.
Wraps Umbraco Forms `FormsFileSystem` by default, but can be inherited to wrap others.

### Installation

Has to be manually built as of sept. 12 2016, but it's just a dll. :)  

### Configuration

As of Umbraco.Forms v. 4.3.3 which hopefully will be released sometime during september,
you can add the following to ~/config/fileSystemProviders.config:

	<Provider alias="forms" type="Our.Umbraco.FileSystemProviders.Vso.VsoGitFileSystemProvider, Our.Umbraco.FileSystemProviders.Vso">
		<Parameters>
			<add key="virtualRoot" value="~/App_Plugins/UmbracoForms/Data/" />
			<add key="gitUrl" value="https://[youraccount].visualstudio.com"/>
			<add key="username" value="[your username]"/>
			<add key="password" value="[token or basic pass]"/>
			<add key="repositoryId" value="[repository id (guid)]"/>
			<add key="repoRoot" value="[path to website root in git]"/>
		</Parameters>
	</Provider>
