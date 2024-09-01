# MHS - Mikhail Home Security

This is the source code that I use for my DIY Security system in my home. 

## Background
My home had an ADT BHS-4000 system originally.

<img src="https://github.com/MishaProductions/SecuritySystem/assets/106913236/27c754fa-6541-4767-92e9-333826467a53" width="200" height="320"/>

(Sorry for horrible camera quality)

This system still had its original sensors and everything, it was just simply unplugged. Around 2022, I replaced the main board with an Orange PI PC+, and that's when this project began. The window sensors simply pass through power when they are closed, and vice versa.

I bought a simple GPIO breakout board, and connected all of the sensors to there.

![PXL_20240704_134330183](https://github.com/MishaProductions/SecuritySystem/assets/106913236/fb660af7-210a-40f9-b868-b60d4197498b)

Nextion Display
![PXL_20240704_141844541](https://github.com/MishaProductions/SecuritySystem/assets/106913236/e720de5c-fb7e-4193-9310-7bed6e164efd)

Web interface / Client

![image](https://github.com/MishaProductions/SecuritySystem/assets/106913236/1922e334-e2f7-42f4-bac8-75ac2193eb4d)

## Installation
Build solution in Visual Studio 2022, publish secsys project for your SOC architecture, and create /usr/lib/systemd/system/secsys.service: 
```
[Unit]
Description=Starts security system
After=multi-user.target
[Service]
ExecStart=/bin/bash /secsys/run.sh
[Install]
WantedBy=multi-user.target

```

Copy the published files to /secsys/ and create /secsys/run.sh:
```
#!/bin/bash
export DOTNET_ROOT=/root/.dotnet
export PATH=$PATH:$DOTNET_ROOT:$DOTNET_ROOT/tools

# Start systemwide Audio server. This is done on boot to prevent noises from speaker
systemctl start pulseaudio

# Start controller
/secsys/SecuritySystem

# Cleanup
systemctl stop pulseaudio
```
After that, create /musics/ and /musics/annc/ directories and install mpv. Enable and start the secsys service. I recommend to set pulseaudio dameon to run globally.
