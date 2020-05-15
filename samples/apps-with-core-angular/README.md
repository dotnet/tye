# Setup Steps

- In order to run this, there are two scenarios. First one is via Dockerfile of angular project and second one via its image. For, first scenario, tye file looks like

```yaml

name: project-tye
services:
- name: moviesapi
  project: MoviesAPI/MoviesAPI.csproj
  bindings:
  - protocol: https
    port: 5001
  
- name: moviesapp
  dockerFile: MoviesApp/Dockerfile
  bindings:
  - protocol: http
    port: 4400

```

- As you can see, .Net docker file will be taken care by the project itself and for angular project, you need to provide <strong>dockerFile</strong> path. 

- Here I have provided port for api and movies app project both. Since, I am using 5001 port in angular app for making a service call. Hence, I specified it explicitly. MoviesApp can have random port number. It depends on you, how you would like to keep it.

- Having said that, first you need to do <strong>tye build .</strong> at the root level of your project to build it. Upon successful build, it should come like

![image](https://user-images.githubusercontent.com/3886381/82045089-1c44ff00-96cc-11ea-8816-c7decf19053b.png)

- Next, we need to run the project with <strong>tye run</strong>. Upon successful execution, it should come like

![image](https://user-images.githubusercontent.com/3886381/82084632-318c4e80-9709-11ea-96c6-c879ffd6ad84.png)

- Then, dashboard http://127.0.0.1:8000/ will appear like

- For Movies app; since, we have used Dockerfile as source, hence its not visible at the moment. In future implementation, it may fetch source for Dockerfile location as well.

![image](https://user-images.githubusercontent.com/3886381/82084718-5d0f3900-9709-11ea-950f-22927c3bad68.png)

- Api can be navigated at https://localhost:5001/api/movies like

![image](https://user-images.githubusercontent.com/3886381/82045641-0edc4480-96cd-11ea-9041-76edc619823a.png)

and Similarly, Movies App can be viewed at http://localhost:4400/movies/ like

![image](https://user-images.githubusercontent.com/3886381/82045821-6084cf00-96cd-11ea-914a-f5ce84530ad7.png)

- For second scenario, 

- First of all you need to create docker image for angular project. Hence, you need to go into MoviesApp project and run the command
<strong>docker build -t moviesapp:dev .</strong>

- Once this done, then you can verify the images with docker images command and it will come something like this:

![image](https://user-images.githubusercontent.com/3886381/82044405-ee12ef80-96ca-11ea-9ced-0ba7b91c43da.png)

- The image which you created earlier can be tagged here. You also need to mention port number explicitly for .net project as this is internally getting used by angular app.


- Next, tye.yaml file looks like as shown below

```yaml
name: project-tye
services:
- name: moviesapi
  project: MoviesAPI/MoviesAPI.csproj
  bindings:
  - protocol: https
    port: 5001
  
- name: moviesapp
  image: moviesapp:dev
  bindings:
  - protocol: http
    port: 4400
```

- Next, you can build and run tye same as explained above.

- Here also you can navigate the dashboard link at the same url http://127.0.0.1:8000/. Only difference is it will show <strong>moviesapp:dev</strong> as source.

![image](https://user-images.githubusercontent.com/3886381/82045390-a725f980-96cc-11ea-96c2-8972f2ee870b.png)

Therefore, we have seen two ways to execute this scenario. You can pick either way. Even for .net project as well, we can have Dockerfile if we would like to do custom changes.

Thanks for joining me. In case if you have any further query, you can reach out to me at https://twitter.com/rahulsahay19 
