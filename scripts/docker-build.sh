#!/usr/bin/env bash
docker build -t registry.itpartner.no/innovasjon/warmup:latest .
[ ! -z $1 ] && docker push registry.itpartner.no/innovasjon/warmup:latest
