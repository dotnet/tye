# VotingSample
Voting sample app inspired by https://github.com/dockersamples/example-voting-app with a few different implementation choices.

## For running

The project should be immediately runnable by calling `tye run` from the directory.

## For deployment

A few things need to be configured before deploying to Kubernetes.

- Setting up Redis. A connection string needs to be provided to connect to Redis. You can follow our tutorial on [setting up redis in your cluster](../../docs/redis.md).
- Setting up postgresql. A connection string eventually needs to be provided to tye for postgresql.
- Deploying the ingress.yaml by calling:

    ```
    kubectl apply -f ingress.yml
    ```
    