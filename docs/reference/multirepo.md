# Using tye with Multiple Repositories

*This document is a conceptual overview of how Tye can be used in multirepo scenarios*

There are many times where an application will consist of multiple repositories. Tye supports this through referencing other tye.yaml or repositories.

## Reference to another tye.yaml

Let's assume we have an app that has a frontend and inventory service. The frontend depends on the inventory service running to keep track of orders.The inventory service uses redis to cache some orders as well.

If we had a single repository, the app would look like the following

```yaml
name: multirepo-app
services:
- name: frontend
   project: ./frontend/frontend.csproj
- name: inventory
   project: ./inventory/inventory.csproj
- name: redis
  container: redis
  bindings:
  - port: 6793
```

Now, let's say that the inventory service was not in the same repository and now in a separate repository, side by side with the frontend repository. You can modify the tye.yaml files in the following way.

```yaml
# frontend's tye.yaml
name: multirepo-app
services:
- name: frontend
   project: ./frontend/frontend.csproj
- name: inventory
   include: ../inventoryRepo/tye.yaml
```

```yaml
# inventory's tye.yaml
name: multirepo-app
services:
- name: inventory
   include: inventory/inventory.csproj
- name: redis
  container: redis
  bindings:
  - port: 6793
```

Now, when you execute `tye run` or `tye deploy` in the frontend repo, it will run/deploy the frontend, as well as the inventory service and redis as there is a reference to the tye.yaml. What is different is that when executing `tye run` or `tye deploy` in the inventory repo, only the inventory and redis services will be ran/deployed.

Another thing to note is that the frontend will not have service discovery information injected for redis, as the frontend doesn't directly depend on redis. If the frontend did depend on redis, they could either add a reference to the redis service in the inventory's tye.yaml, or add a reference to redis directly in their own tye.yaml.

When evaluating service dependencies, if duplicate services are encountered, the first/closest wins in a breath first ordering.

## Reference to git repository

Tye can use dependencies which are referenced via a git repository as well. In the above example, instead of the inventory service being present, let's change the reference to be a repository.

```yaml
# frontend's tye.yaml
name: multirepo-app
services:
- name: frontend
   project: ./frontend/frontend.csproj
- name: inventory
   repository: https://github.com/MyOrg/InventoryRepo
```

The repository cloned will follow the same conventions for figuring out how to run/deploy; either using a tye.yaml, sln, then csproj/fsproj if present. These are all based on the root of the clone repository.

The string for `repository` is what should be passed to the call to `git clone`. So form example, if you'd like to specify a branch to clone, you can specify `--branch`.

```yaml
repository: https://github.com/MyOrg/InventoryRepo --branch release/1.0
```

Dependencies will be cloned to the .tye/deps folder inside of the repo.
