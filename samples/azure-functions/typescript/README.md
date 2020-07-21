# Typescript example
A simple example showing that tye supports running non-dotnet azure functions locally. 

## For running

Before running, navigate to the HttpExample and run `npm install`. Run `npm start` as well to verify the function starts without tye.

Next, all you need to do is execute `tye run` and navigate to the dashboard. Navigate to <SERVICE_URL>/api/HttpExample to see the function working.

> :bulb: Note, you may need to create a local.settings.json file to specify the language default. Tye currently doesn't specify the language by default.