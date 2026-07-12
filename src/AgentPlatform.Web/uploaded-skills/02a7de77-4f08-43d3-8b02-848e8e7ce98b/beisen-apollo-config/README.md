# Apollo配置助手

帮助理解灰度配置文件，分析代码中的灰度分支逻辑。

## 功能

1. **理解灰度配置** - 读取配置文件，解释某个灰度功能对哪些租户开放
2. **分析代码分支** - 当代码中有 `AllowTenant` 调用时，帮助理解走哪个分支
3. **生成.cs文件** - 必要时根据配置文件生成C#映射类

## 使用示例

### 理解灰度配置

```
用户: EnableAIPermisison 这个灰度开了哪些租户？

AI: 根据 GrayControlConfig.xml 配置：
- 启用状态: 已启用
- 全部租户: 是
- 白名单租户: 600113
- 结论: 该功能对所有租户开放
```

### 分析代码分支

```
用户: 这段代码的灰度逻辑是什么？
if (GrayControlConfig.Instance.AllowTenant(GrayControlConfig.EnableSteppe, tenantId))
{
    // 走Steppe逻辑
}

AI: 根据配置文件，EnableSteppe 的灰度配置是：
- 白名单: 110006
- 号段: 4开头的租户
- 所以租户ID为 110006 或 4xxxxx 的会走Steppe逻辑
```

### 生成.cs文件

```bash
python scripts/generate_gray_config.py "C:\beisen.configs\Dev\GrayControlConfig.xml" "GrayControlConfig.cs"
```

## 配置文件位置

- Dev: `C:\beisen.configs\Dev`
- Test: `C:\beisen.configs\Test`
