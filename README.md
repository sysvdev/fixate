# Fixate
Highly configurable bot to announce raid boss mechanics


### Docker support

```docker
version: '3.4'

services:
  fixate:
    image: ${DOCKER_REGISTRY-}fixate
    build:
      context: .
      dockerfile: ./Dockerfile
    environment: 
      - Discord_Token=<token>
      - Discord_CommandPrefix=!
      - Voice_APIToken=<token>
      - Voice_APIRegion=eastus
      - Voice_Language=en-US
      - Voice_Name=en-US-JennyNeural
      - Voice_Style=chat
```