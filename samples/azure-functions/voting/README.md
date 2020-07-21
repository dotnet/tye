# VotingSample
Voting sample app inspired by https://github.com/dockersamples/example-voting-app with a few different implementation choices. This voting app uses [Azure Functions](https://azure.microsoft.com/en-us/services/functions/) with a Queue and Http function.

## For running

To run, first make sure the azure storage emulator is running. You can use [Azurite](https://docs.microsoft.com/en-us/azure/storage/common/storage-use-azurite) cross platform or use the [Azure Storage Emulator](https://docs.microsoft.com/en-us/azure/storage/common/storage-use-emulator?toc=/azure/storage/blobs/toc.json) on Windows.

Next, all you need to do is execute `tye run` and navigate to the dashboard.

> :bulb: Note, you may need to create a [local.settings.json](https://docs.microsoft.com/en-us/azure/azure-functions/functions-run-local?tabs=windows%2Ccsharp%2Cbash#local-settings-file) file to specify the language default. Tye currently doesn't specify the language by default.

## For deployment

Deployment is currently not supported for Azure Functions.