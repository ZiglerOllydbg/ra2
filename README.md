# ra2
红警项目研究

## 项目结构

底层基础
    数学底层
    GamePlay底层
        寻路
            团队寻路
            RVO等支持
        AI
            简化点 可以先基于简易规则
        地图
            分层
                寻路分层 水层、陆地层、两栖层、空中等
                资源层
                地形

## 🚀 自动构建和发布

本项目已配置 GitHub Actions 构建，支持：

- ✅ Android APK 构建（Linux，快速且低成本）
- ✅ iOS IPA 构建（两阶段优化：Linux 导出 + macOS 编译，节省 67% 成本）
- ✅ 自动上传到蒲公英平台
- ✅ 手动触发构建，可自定义版本说明

### 快速开始

1. **配置 GitHub Secrets**: 查看 [.github/SETUP.md](.github/SETUP.md) 了解详细配置步骤

2. **手动触发构建**: 
   - 进入 GitHub 仓库的 `Actions` 标签
   - 选择要构建的平台（Android 或 iOS）
   - 点击 `Run workflow` 按钮
   - 可选填写版本名称

3. **下载安装**: 构建完成后，在 Actions 日志中获取蒲公英下载链接

详细配置说明请查看: [GitHub Actions 配置文档](.github/SETUP.md)

