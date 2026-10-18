# 型号适配配置

`monitors.xml` 按 Windows 硬件型号（例如 PHLC25B）精确匹配；同品牌不自动套用同一组配置。未命中配置时仍使用 WMI 型号和通用能力检测。

```xml
<MonitorProfiles>
  <Profile hardware="EXAMPLE123" model="Example display">
    <Value code="14" value="05" label="色彩 · 6500 K" />
  </Profile>
</MonitorProfiles>
```

`code` 和 `value` 是不带 0x 的十六进制值。只有显示器明确声明、且当前代码能够读取时，标签才生效。配置不会加入额外指令或值。这样社区可维护名称，而不会扩大硬件写入范围。

请附诊断和 OSD 菜单验证证据后提交名称；未经验证的画面模式保持原始编号。本版仅开放标准 14/DC，厂商扩展需要另行实现与审查，不接受通过 XML 注入私有写入。

同型号不同固件仍可能不同，因此配置不取代运行时能力检测和回读验证。
