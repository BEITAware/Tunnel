#!/usr/bin/env python3
"""
递归移除文件夹下所有文本文件中以 Console.WriteLine 开头的行
"""

import os
import re
import argparse
from pathlib import Path

def is_text_file(file_path):
    """判断文件是否为文本文件"""
    text_extensions = {
        '.cs', '.txt', '.log', '.xml', '.json', '.config', '.xaml', 
        '.js', '.ts', '.html', '.css', '.md', '.yml', '.yaml',
        '.cpp', '.h', '.hpp', '.c', '.py', '.java', '.kt'
    }
    return file_path.suffix.lower() in text_extensions

def remove_console_lines(file_path, dry_run=False):
    """移除文件中的Console输出行"""
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            lines = f.readlines()
    except UnicodeDecodeError:
        try:
            with open(file_path, 'r', encoding='gbk') as f:
                lines = f.readlines()
        except UnicodeDecodeError:
            print(f"⚠️  无法读取文件 {file_path} (编码问题)")
            return 0
    except Exception as e:
        print(f"⚠️  读取文件失败 {file_path}: {e}")
        return 0

    # 匹配 Console.WriteLine 相关的行（考虑缩进和各种变体）
    console_patterns = [
        re.compile(r'^\s*Console\.WriteLine.*$', re.MULTILINE),
        re.compile(r'^\s*Console\.Write\(.*$', re.MULTILINE),
        re.compile(r'^\s*console\.writeline.*$', re.MULTILINE | re.IGNORECASE),
        re.compile(r'^\s*console\.write\(.*$', re.MULTILINE | re.IGNORECASE),
    ]
    
    original_count = len(lines)
    filtered_lines = []
    removed_count = 0
    
    for line in lines:
        should_remove = False
        for pattern in console_patterns:
            if pattern.match(line):
                should_remove = True
                break
        
        if should_remove:
            removed_count += 1
            print(f"🗑️  移除: {line.strip()}")
        else:
            filtered_lines.append(line)
    
    if removed_count > 0:
        if not dry_run:
            try:
                with open(file_path, 'w', encoding='utf-8') as f:
                    f.writelines(filtered_lines)
                print(f"✅ 处理完成: {file_path} (移除 {removed_count} 行)")
            except Exception as e:
                print(f"⚠️  写入文件失败 {file_path}: {e}")
                return 0
        else:
            print(f"🔍 [预览模式] {file_path} 将移除 {removed_count} 行")
    
    return removed_count

def process_directory(directory, dry_run=False):
    """递归处理目录下的所有文本文件"""
    directory = Path(directory)
    if not directory.exists():
        print(f"❌ 目录不存在: {directory}")
        return
    
    total_files = 0
    total_removed = 0
    processed_files = 0
    
    print(f"🔍 开始扫描目录: {directory}")
    print(f"{'🔍 [预览模式] ' if dry_run else ''}正在处理文件...")
    print("-" * 60)
    
    for file_path in directory.rglob('*'):
        if file_path.is_file() and is_text_file(file_path):
            total_files += 1
            removed_count = remove_console_lines(file_path, dry_run)
            if removed_count > 0:
                processed_files += 1
                total_removed += removed_count
    
    print("-" * 60)
    print(f"📊 处理统计:")
    print(f"   扫描文件总数: {total_files}")
    print(f"   处理文件数量: {processed_files}")
    print(f"   移除Console行数: {total_removed}")
    
    if dry_run:
        print("\n💡 这是预览模式，没有实际修改文件。")
        print("   如需实际执行，请移除 --dry-run 参数。")

def main():
    parser = argparse.ArgumentParser(
        description="递归移除文件夹下所有文本文件中以 Console.WriteLine 开头的行"
    )
    parser.add_argument(
        'directory', 
        nargs='?', 
        default='.', 
        help='要处理的目录路径 (默认: 当前目录)'
    )
    parser.add_argument(
        '--dry-run', 
        action='store_true', 
        help='预览模式，只显示将要移除的行，不实际修改文件'
    )
    
    args = parser.parse_args()
    
    print("🧹 Console输出清理工具")
    print("=" * 60)
    
    process_directory(args.directory, args.dry_run)

if __name__ == "__main__":
    main()
