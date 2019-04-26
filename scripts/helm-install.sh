#!/usr/bin/env bash

helm install \
    -f helm/values.yaml \
    --name warmup \
    --namespace default \
    ./helm $@
