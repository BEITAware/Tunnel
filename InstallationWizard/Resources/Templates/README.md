# Tunnel 模板系统说明

## 模板目录结构

每个模板都是一个独立的文件夹，包含以下文件：

```
模板名称/
├── 模板名称.nodegraph    # 节点图文件（必需）
└── thumbnail.png         # 缩略图（可选，推荐尺寸：256x256）
```

## 模板文件格式

`.nodegraph` 文件是 JSON 格式，包含以下结构：

```json
{
  "version": "2.0",
  "name": "模板名称",
  "lastModified": "None",
  "viewport": {
    "x": 0.0,
    "y": 0.0,
    "zoomLevel": 1.0
  },
  "metadata": {},
  "nodes": [],
  "connections": []
}
```

## 添加新模板

1. 在此目录下创建新文件夹，文件夹名称即为模板名称
2. 在文件夹中创建 `.nodegraph` 文件
3. （可选）添加 `thumbnail.png` 缩略图
4. 重新构建 InstallationWizard 项目

## 安装后的模板位置

安装程序会将模板复制到：
`%USERPROFILE%\Documents\TNX\Resources\Templates\`

用户可以在此位置手动添加或修改模板。

## 注意事项

- 模板文件夹名称应避免使用特殊字符
- 缩略图推荐使用 PNG 格式，尺寸 256x256 像素
- 节点图文件必须是有效的 JSON 格式
- 每个模板文件夹必须至少包含一个 `.nodegraph` 文件
