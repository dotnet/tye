#!/usr/bin/env bash

source="${BASH_SOURCE[0]}"

# resolve $SOURCE until the file is no longer a symlink
while [[ -h $source ]]; do
  scriptroot="$( cd -P "$( dirname "$source" )" && pwd )"
  source="$(readlink "$source")"

  # if $source was a relative symlink, we need to resolve it relative to the path where the
  # symlink file was located
  [[ $source != /* ]] && source="$scriptroot/$source"
done

scriptroot="$( cd -P "$( dirname "$source" )" && pwd )"

rm -rf "$scriptroot/../../artifacts/tye-diag-agent-publish"
dotnet publish "$scriptroot" \
    -c Release \
    -o "$scriptroot/../../artifacts/tye-diag-agent-publish" \
    -r linux-x64 --self-contained false

if [ -z "$1" ]; then
  docker build "$scriptroot/../../artifacts/tye-diag-agent-publish" -f "$scriptroot/Dockerfile"
else
  docker build "$scriptroot/../../artifacts/tye-diag-agent-publish" -f "$scriptroot/Dockerfile" -t "rynowak/tye-diag-agent:$1"
fi 