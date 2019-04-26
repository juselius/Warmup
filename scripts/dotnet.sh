#!/usr/bin/env bash

d=`readlink -f $0`
dir=`dirname $d`
name=`basename $d`
dotnet $dir/$name.dll $*
