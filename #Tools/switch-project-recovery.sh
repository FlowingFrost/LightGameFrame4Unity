#!/bin/bash
# 切换 ProjectRecovery 在源码模式和 DLL 模式之间
# 用法: ./switch-project-recovery.sh [dll|source]

set -e

PROJECT_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PLUGIN_DIR="$PROJECT_ROOT/Assets/Plugins/ProjectRecovery"
EDITOR_DIR="$PLUGIN_DIR/Editor"
BACKUP_DIR="$PROJECT_ROOT/#Backup/ProjectRecovery"
DLL_SRC="$PROJECT_ROOT/Library/ScriptAssemblies/ProjectRecovery.dll"
ASMDEF="$PLUGIN_DIR/ProjectRecovery.asmdef"
ASMDEF_TEMPLATE='{
    "name": "ProjectRecovery",
    "rootNamespace": "",
    "references": [],
    "includePlatforms": ["Editor"],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": false,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}'

# 检测当前模式
if [ -f "$EDITOR_DIR/ProjectRecovery.dll" ]; then
    CURRENT="dll"
elif [ -f "$EDITOR_DIR/RecoveryHelper.cs" ]; then
    CURRENT="source"
else
    CURRENT="unknown"
fi

TARGET="${1:-}"

if [ -z "$TARGET" ]; then
    echo "当前模式: $CURRENT"
    echo "用法: $0 [dll|source]"
    echo "  dll    - 切换到 DLL 模式（从 Library 复制编译产物）"
    echo "  source - 切换到源码模式（从备份恢复 .cs）"
    exit 0
fi

if [ "$TARGET" = "$CURRENT" ]; then
    echo "已经是 $CURRENT 模式，无需切换"
    exit 0
fi

case "$TARGET" in
    dll)
        # 确保 DLL 存在
        if [ ! -f "$DLL_SRC" ]; then
            echo "错误: $DLL_SRC 不存在，请先在 Unity 中编译项目"
            exit 1
        fi

        # 备份源码
        mkdir -p "$BACKUP_DIR"
        cp "$EDITOR_DIR/RecoveryHelper.cs" "$BACKUP_DIR/"
        cp "$EDITOR_DIR/RecoveryWindow.uxml" "$BACKUP_DIR/"
        cp "$EDITOR_DIR/RecoveryWindow.uss" "$BACKUP_DIR/"

        # 复制 DLL
        cp "$DLL_SRC" "$EDITOR_DIR/"

        # 删除源码和 asmdef
        rm -f "$EDITOR_DIR/RecoveryHelper.cs" "$EDITOR_DIR/RecoveryHelper.cs.meta"
        rm -f "$ASMDEF" "$ASMDEF.meta"

        echo "已切换到 DLL 模式"
        echo "  源码已备份到 #Backup/ProjectRecovery/"
        echo "  DLL 已复制到 Editor/"
        ;;
    source)
        # 确保备份存在
        if [ ! -f "$BACKUP_DIR/RecoveryHelper.cs" ]; then
            echo "错误: 备份目录中没有 RecoveryHelper.cs"
            exit 1
        fi

        # 恢复源码
        cp "$BACKUP_DIR/RecoveryHelper.cs" "$EDITOR_DIR/"
        cp "$BACKUP_DIR/RecoveryWindow.uxml" "$EDITOR_DIR/"
        cp "$BACKUP_DIR/RecoveryWindow.uss" "$EDITOR_DIR/"

        # 恢复 asmdef
        echo "$ASMDEF_TEMPLATE" > "$ASMDEF"

        # 删除 DLL
        rm -f "$EDITOR_DIR/ProjectRecovery.dll"

        echo "已切换到源码模式"
        echo "  源码已从备份恢复"
        echo "  asmdef 已重建"
        ;;
    *)
        echo "未知模式: $TARGET（可选: dll, source）"
        exit 1
        ;;
esac
