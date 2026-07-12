#!/usr/bin/env python3
"""
灰度控制配置生成器
专门处理GrayControlConfig.xml格式的配置文件
"""

import os
import sys
import xml.etree.ElementTree as ET
from pathlib import Path
from typing import List, Dict, Any

class GrayConfigGenerator:
    """灰度控制配置生成器"""

    def __init__(self, namespace: str = "Beisen.UserFramework.RemoteConfiguration"):
        self.namespace = namespace

    def generate_from_xml(self, xml_file: Path) -> str:
        """从GrayControlConfig.xml生成C#类"""
        tree = ET.parse(xml_file)
        root = tree.getroot()

        # 解析所有灰度控制条目
        gray_controls = []
        gray_controls_elem = root.find("GrayControls")
        if gray_controls_elem is not None:
            for item in gray_controls_elem.findall("GrayControlConfigItem"):
                name = item.get("Name")
                if name:
                    gray_controls.append(name)

        # 生成C#类代码
        return self._generate_gray_config_class(gray_controls)

    def _generate_gray_config_class(self, gray_controls: List[str]) -> str:
        """生成灰度控制配置类"""
        lines = []

        # 添加using语句
        lines.append("using Beisen.Configuration;")
        lines.append("using System;")
        lines.append("using System.Collections.Generic;")
        lines.append("using System.Linq;")
        lines.append("using System.Xml.Serialization;")
        lines.append("")
        lines.append(f"namespace {self.namespace}")
        lines.append("{")
        lines.append("    [XmlRoot(\"GrayControlConfig\")]")
        lines.append("    [Serializable]")
        lines.append("    public class GrayControlConfig : BaseConfig<GrayControlConfig>")
        lines.append("    {")

        # 添加灰度控制条目列表
        lines.append("        [XmlArray(\"GrayControls\")]")
        lines.append("        [XmlArrayItem(\"GrayControlConfigItem\")]")
        lines.append("        public List<GrayControlConfigItem> GrayControls { get; set; } = new List<GrayControlConfigItem>();")
        lines.append("")

        # 添加灰度控制条目名称常量
        lines.append("        #region 灰度控制条目名称常量")
        lines.append("")
        for control_name in gray_controls:
            lines.append(f"        /// <summary>")
            lines.append(f"        /// {control_name}")
            lines.append(f"        /// </summary>")
            lines.append(f"        public const string {control_name} = \"{control_name}\";")
            lines.append("")
        lines.append("        #endregion")
        lines.append("")

        # 添加AllowTenant方法
        lines.append("        #region 灰度判断方法")
        lines.append("")
        lines.append("        /// <summary>")
        lines.append("        /// 判断租户是否在灰度范围内")
        lines.append("        /// </summary>")
        lines.append("        /// <param name=\"itemName\">灰度控制条目名称</param>")
        lines.append("        /// <param name=\"tenantId\">租户ID</param>")
        lines.append("        /// <returns>是否在灰度范围内</returns>")
        lines.append("        public bool AllowTenant(string itemName, int tenantId)")
        lines.append("        {")
        lines.append("            var item = GrayControls.Find(x => x.Name == itemName);")
        lines.append("            if (item == null || !item.EnableGrayControl)")
        lines.append("                return false;")
        lines.append("")
        lines.append("            // 黑名单优先")
        lines.append("            if (item.EnableBlockTenantIds && item.BlockTenantIds != null && item.BlockTenantIds.Contains(tenantId))")
        lines.append("                return false;")
        lines.append("")
        lines.append("            // 黑名单号段")
        lines.append("            if (item.EnableRangeBlockTenantId && item.RangeBlockTenantIds != null)")
        lines.append("            {")
        lines.append("                foreach (var range in item.RangeBlockTenantIds)")
        lines.append("                {")
        lines.append("                    if (range.StartNode <= tenantId && range.EndNode >= tenantId)")
        lines.append("                        return false;")
        lines.append("                }")
        lines.append("            }")
        lines.append("")
        lines.append("            // 全部租户允许")
        lines.append("            if (item.EnableAllowAllTenant)")
        lines.append("                return true;")
        lines.append("")
        lines.append("            // 白名单匹配")
        lines.append("            if (item.EnableAllowTenantIds && item.AllowTenantIds != null && item.AllowTenantIds.Contains(tenantId))")
        lines.append("                return true;")
        lines.append("")
        lines.append("            // 号段匹配")
        lines.append("            if (item.EnableAllowTenantIdStartNum && item.AllowTenantIdStartNums != null)")
        lines.append("            {")
        lines.append("                int startNum = tenantId / 100000;")
        lines.append("                if (item.AllowTenantIdStartNums.Contains(startNum))")
        lines.append("                    return true;")
        lines.append("            }")
        lines.append("")
        lines.append("            // 白名单号段")
        lines.append("            if (item.EnableRangeAllowTenantId && item.RangeAllowTenantIds != null)")
        lines.append("            {")
        lines.append("                foreach (var range in item.RangeAllowTenantIds)")
        lines.append("                {")
        lines.append("                    if (range.StartNode <= tenantId && range.EndNode >= tenantId)")
        lines.append("                        return true;")
        lines.append("                }")
        lines.append("            }")
        lines.append("")
        lines.append("            return false;")
        lines.append("        }")
        lines.append("")
        lines.append("        /// <summary>")
        lines.append("        /// 获取灰度控制条目")
        lines.append("        /// </summary>")
        lines.append("        public GrayControlConfigItem GetItem(string itemName)")
        lines.append("        {")
        lines.append("            return GrayControls.Find(x => x.Name == itemName);")
        lines.append("        }")
        lines.append("")
        lines.append("        #endregion")
        lines.append("    }")
        lines.append("")

        # 添加GrayControlConfigItem类
        lines.append("    [Serializable]")
        lines.append("    public class GrayControlConfigItem")
        lines.append("    {")
        lines.append("        [XmlAttribute(\"Name\")]")
        lines.append("        public string Name { get; set; }")
        lines.append("")
        lines.append("        [XmlAttribute(\"EnableGrayControl\")]")
        lines.append("        public bool EnableGrayControl { get; set; }")
        lines.append("")
        lines.append("        [XmlAttribute(\"EnableAllowTenantIds\")]")
        lines.append("        public bool EnableAllowTenantIds { get; set; }")
        lines.append("")
        lines.append("        [XmlAttribute(\"EnableBlockTenantIds\")]")
        lines.append("        public bool EnableBlockTenantIds { get; set; }")
        lines.append("")
        lines.append("        [XmlAttribute(\"EnableAllowTenantIdStartNum\")]")
        lines.append("        public bool EnableAllowTenantIdStartNum { get; set; }")
        lines.append("")
        lines.append("        [XmlAttribute(\"EnableAllowAllTenant\")]")
        lines.append("        public bool EnableAllowAllTenant { get; set; }")
        lines.append("")
        lines.append("        [XmlAttribute(\"EnableRangeAllowTenantId\")]")
        lines.append("        public bool EnableRangeAllowTenantId { get; set; }")
        lines.append("")
        lines.append("        [XmlAttribute(\"EnableRangeBlockTenantId\")]")
        lines.append("        public bool EnableRangeBlockTenantId { get; set; }")
        lines.append("")
        lines.append("        [XmlAttribute(\"EnableGrayMultiWrite\")]")
        lines.append("        public bool EnableGrayMultiWrite { get; set; }")
        lines.append("")
        lines.append("        [XmlArray(\"AllowTenantIds\")]")
        lines.append("        [XmlArrayItem(\"AllowTenantId\")]")
        lines.append("        public HashSet<int> AllowTenantIds { get; set; } = new HashSet<int>();")
        lines.append("")
        lines.append("        [XmlArray(\"BlockTenantIds\")]")
        lines.append("        [XmlArrayItem(\"BlockTenantId\")]")
        lines.append("        public HashSet<int> BlockTenantIds { get; set; } = new HashSet<int>();")
        lines.append("")
        lines.append("        [XmlArray(\"AllowTenantIdStartNums\")]")
        lines.append("        [XmlArrayItem(\"AllowTenantIdStartNum\")]")
        lines.append("        public HashSet<int> AllowTenantIdStartNums { get; set; } = new HashSet<int>();")
        lines.append("")
        lines.append("        [XmlArray(\"RangeAllowTenantIds\")]")
        lines.append("        [XmlArrayItem(\"RangeTenantId\")]")
        lines.append("        public List<RangeTenantId> RangeAllowTenantIds { get; set; } = new List<RangeTenantId>();")
        lines.append("")
        lines.append("        [XmlArray(\"RangeBlockTenantIds\")]")
        lines.append("        [XmlArrayItem(\"RangeTenantId\")]")
        lines.append("        public List<RangeTenantId> RangeBlockTenantIds { get; set; } = new List<RangeTenantId>();")
        lines.append("    }")
        lines.append("")

        # 添加RangeTenantId类
        lines.append("    [Serializable]")
        lines.append("    public class RangeTenantId")
        lines.append("    {")
        lines.append("        [XmlAttribute(\"StartNode\")]")
        lines.append("        public int StartNode { get; set; }")
        lines.append("")
        lines.append("        [XmlAttribute(\"EndNode\")]")
        lines.append("        public int EndNode { get; set; }")
        lines.append("    }")
        lines.append("}")

        return "\n".join(lines)


def main():
    """主函数"""
    if len(sys.argv) < 3:
        print("Usage: python generate_gray_config.py <xml_file> <output_file>")
        print("Example: python generate_gray_config.py C:\\beisen.configs\\Dev\\GrayControlConfig.xml C:\\output\\GrayControlConfig.cs")
        sys.exit(1)

    xml_file = Path(sys.argv[1])
    output_file = Path(sys.argv[2])

    if not xml_file.exists():
        print(f"Error: XML file not found: {xml_file}")
        sys.exit(1)

    generator = GrayConfigGenerator()
    csharp_code = generator.generate_from_xml(xml_file)

    # 确保输出目录存在
    output_file.parent.mkdir(parents=True, exist_ok=True)

    # 写入文件
    with open(output_file, 'w', encoding='utf-8') as f:
        f.write(csharp_code)

    print(f"Generated: {output_file}")


if __name__ == "__main__":
    main()
