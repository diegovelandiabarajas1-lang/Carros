#!/bin/sh
printf '\033c\033]0;%s\a' Nuevo Proyecto de Juego
base_path="$(dirname "$(realpath "$0")")"
"$base_path/Carro.x86_64" "$@"
