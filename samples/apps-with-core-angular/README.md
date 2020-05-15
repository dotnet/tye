# Setup Steps

- First of all you need to create docker image for angular project. Hence, you need to go into MoviesApp project and run the command
<strong>docker build -t moviesapp:dev .</strong>

- Once this done, then you can verify the images with docker images command and it will come something like this:

![image](https://user-images.githubusercontent.com/3886381/82044405-ee12ef80-96ca-11ea-9ced-0ba7b91c43da.png)

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

- As you can see, .Net docker file will be taken care by tye itself and for angular project, you need to have image. The image which you created earlier can be tagged here. You also need to mention port number explicitly for .net project as this is internally getting used by angular app.

- MoviesApp can have random port number. It depends on you, how you would like to keep it.

- Having said that, first you need to do <strong>tye build .</strong> at the root level of your project to build it. Upon successful build, it should come like

![image](https://user-images.githubusercontent.com/3886381/82045089-1c44ff00-96cc-11ea-8816-c7decf19053b.png)

- Then, you can run the app with <strong>tye run</strong> command.

![image](https://user-images.githubusercontent.com/3886381/82045221-5a422300-96cc-11ea-9ae5-b0339ea0a31f.png)

- Now, you can navigate to the dashboard at the link http://127.0.0.1:8000/

![image](https://user-images.githubusercontent.com/3886381/82045390-a725f980-96cc-11ea-96c2-8972f2ee870b.png)

- Api can be navigated https://localhost:5001/api/movies like

![image](https://user-images.githubusercontent.com/3886381/82045641-0edc4480-96cd-11ea-9041-76edc619823a.png)

and Similarly, Movies App can be viewed at http://localhost:4400/movies/ like

![image](https://user-images.githubusercontent.com/3886381/82045821-6084cf00-96cd-11ea-914a-f5ce84530ad7.png)

Thanks for joining me. In case if you have any further query, you can reach out to me at https://twitter.com/rahulsahay19 
