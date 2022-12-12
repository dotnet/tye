#!/usr/bin/env bash

versionprefix=`awk -F'[<>]' '/VersionPrefix.*VersionPrefix/{print $3}' ./eng/Versions.props`

dotnet tool install microsoft.tye -g --version "$versionprefix-dev" --add-source ./artifacts/packages/Debug/Shipping
