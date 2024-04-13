## From the server directory:

### Game hub
Build: `docker build -t belter-gamehub:latest -f .\src\Belter.GameHub\Dockerfile ./src`  
Run: `docker run -it -p 8080:8080 belter-gamehub`  

### Game server
Build: `docker build -t belter-gameserver:latest -f .\src\Belter.GameServer\Dockerfile ./src`  
Run: `docker run -it -e DOTNET_ENVIRONMENT=Development belter-gameserver`  
