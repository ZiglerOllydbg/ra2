#!/bin/bash

echo "Starting ra2 server..."
echo "Using system Java"

# 检查Java是否存在
if ! command -v java &> /dev/null; then
    echo "Error: Java executable not found in PATH"
    echo "Please check your Java installation and PATH setting"
    exit 1
fi

# 运行Java程序
java -Dfile.encoding=UTF-8 -cp "ra2.jar:config/*:libs/*:" org.game.ra2.GameStartUp

# 检查运行结果
if [ $? -ne 0 ]; then
    echo "Failed to start ra2 server."
    exit 1
fi