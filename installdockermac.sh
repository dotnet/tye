brew cask install docker
set -x
echo "Running xattr"
xattr -d -r com.apple.quarantine /Applications/Docker.app 
echo "Running Docker as root to install helpers"
sudo -b /Applications/Docker.app/Contents/MacOS/Docker
echo "Taking a break to let Docker UI open"
# an improvement over this blind sleep would be using osascript to wait until Docker is visible and then run the next step
sleep 15
echo "Hit next on startup wizard"
osascript -s o -e 'tell application "Docker"' -e 'activate' -e 'tell application "System Events" to key code 36' -e 'end tell'
echo "Waiting for docker system info to respond (which means the service is available)"
set +x
sleep 5
# if the script appears to hang for more than 3 minutes comment above `set +x` or try to run the command inside the loop to see the output, like 'do sleep 1; sudo docker system info; done'
while ! sudo /Applications/Docker.app/Contents/Resources/bin/docker system info &gt; /dev/null 2&gt;&1; do sleep 1; done;
echo "Docker is ready and available"
set -x
sudo docker ps
# note the permissions on the files and where the docker.sock is located
ls -lA /usr/local/bin/docker* /var/run/docker*
sleep 1
sudo docker system info
echo "Docker install complete"