# SAFE Authentication with Active Directory.

This shows how you can get everything set up including

- Webpack config
- FAKE build script
- ASP.NET routing

You will need to set up an App Registration in your Active Directory and then paste your Client ID / Tenant ID / Domain into appsettings.json.

The app registration should have a Web platform added with

https://{your app name}.azurewebsites.net/api/login-callback

as its redirect URI and

https://localhost:8085/api/logout-callback

as the front-channel logout URL.

Also tick "Id tokens" under "Implicit grant and hybrid flows".

