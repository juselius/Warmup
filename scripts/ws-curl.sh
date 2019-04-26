#!/bin/sh

host=$1; shift

curl -i -N \
    -H "Connection: upgrade"\
    -H "Upgrade: websocket"\
    -H "Sec-WebSocket-Key: SGVsbG8sIHdvcmxkIQ=="\
    -H "Sec-WebSocket-Version: 13"\
    -H "Origin: http://localhost:8085/"\
    -H "Host: $host" $@

