---
name: beisen-apollo-config
description: 当需要理解灰度配置、分析代码中的灰度分支、或根据配置文件生成.cs映射类时使用。配置文件位于 C:\beisen.configs\Dev 和 C:\beisen.configs\Test 目录。当代码中出现 AllowTenant、GrayControl、灰度判断等关键词时，应主动读取配置文件帮助理解逻辑。
---

# Apollo配置助手

帮助理解灰度配置文件，分析代码中的灰度分支逻辑，必要时生成.cs映射类。

## 配置文件位置

- **Dev环境**: `C:\beisen.configs\Dev`
- **Test环境**: `C:\beisen.configs\Test`

## 使用场景

### 场景1: 理解代码中的灰度逻辑

当代码中出现类似以下调用时：
```csharp
GrayControlConfig.Instance.AllowTenant(GrayControlConfig.SomeFeature, tenantId)
```

**做法**:
1. 读取对应的配置文件（如 `GrayControlConfig.xml`）
2. 找到 `SomeFeature` 这个配置项
3. 分析它的白名单、黑名单、号段等配置
4. 告诉用户：这个功能目前对哪些租户开放

### 场景2: 生成.cs映射类

当用户明确要求生成.cs文件时，使用Python脚本：
```bash
python scripts/generate_gray_config.py <xml_file> <output_file>
```

## 配置文件结构速查

### GrayControlConfig.xml (灰度控制)

```xml
<GrayControlConfig>
  <GrayControls>
    <GrayControlConfigItem 
      Name="功能名称"           <!-- 灰度条目名称 -->
      EnableGrayControl="true"  <!-- 是否启用灰度 -->
      EnableAllowTenantIds="true"  <!-- 是否启用白名单 -->
      EnableBlockTenantIds="false" <!-- 是否启用黑名单 -->
      EnableAllowAllTenant="false" <!-- 是否全部租户开放 -->
      EnableAllowTenantIdStartNum="false"> <!-- 是否启用号段匹配 -->
      
      <AllowTenantIds>          <!-- 白名单租户ID -->
        <AllowTenantId>600113</AllowTenantId>
      </AllowTenantIds>
      <BlockTenantIds>          <!-- 黑名单租户ID -->
        <BlockTenantId>100000</BlockTenantId>
      </BlockTenantIds>
      <AllowTenantIdStartNums>  <!-- 号段匹配 (租户ID前缀) -->
        <AllowTenantIdStartNum>4</AllowTenantIdStartNum>
      </AllowTenantIdStartNums>
    </GrayControlConfigItem>
  </GrayControls>
</GrayControlConfig>
```

### 灰度判断优先级

1. **黑名单** → 命中则返回false
2. **全部租户开放** → 返回true
3. **白名单** → 命中则返回true
4. **号段匹配** → 租户ID/100000 命中则返回true

### JSON格式配置

如 `iTalent.GrayTenantSetting.json`:
```json
{
  "IsEnable": true,        // 是否启用
  "IsAllTenant": true,     // 是否全部租户
  "GrayTenants": [600085]  // 灰度租户列表
}
```

## 示例: 分析灰度配置

用户问: "EnableAIPermisison 这个灰度开了哪些租户？"

**回答模板**:
> 根据 `C:\beisen.configs\Dev\GrayControlConfig.xml` 配置：
> - **启用状态**: 已启用 (EnableGrayControl=true)
> - **全部租户**: 是 (EnableAllowAllTenant=true)
> - **白名单租户**: 600113
> - **黑名单**: 无
> 
> 结论: 该功能对所有租户开放，额外白名单 600113 也包含在内。

## 生成.cs文件

当用户明确需要时：
```bash
python scripts/generate_gray_config.py "C:\beisen.configs\Dev\GrayControlConfig.xml" "输出路径\GrayControlConfig.cs"
```
