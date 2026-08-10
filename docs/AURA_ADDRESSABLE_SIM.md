# Aura Addressable 模拟（实验）

## 这是什么

按 [OpenRGB 公开 wiki：ASUS Aura USB](https://openrgb-wiki.readthedocs.io/en/latest/asus/ASUS-Aura-USB/) 中的报文形状，在软件中实现 **设备端（Device-side）** 状态机，模拟奥创生态中的 **可寻址灯带控制器通道**（Addressable Header / 外置 Terminal 一类），而不是 DRAM 内存。

| 概念 | 说明 |
|------|------|
| 模拟对象 | Aura **Addressable HID 控制器**（USB 侧） |
| 不是 | 磁吸物理灯条本体、也不是 SMBus 杂牌内存 |
| 协议参考 | 公开 wiki：`0xEC` magic、固件查询、`0x35` start update、`0x36` set colors 等 |
| 合规 | clean-room 重写；**未复制** OpenRGB（GPLv2）源码 |

## 与杂牌内存的区别

杂牌 RGB 内存多半是 **SMBus 上的 ENE/Aura DRAM 芯片**；奥创扫 DIMM 总线。  
可寻址灯带在奥创里多半是 **板载/外置 Aura USB 控制器上的 Addressable 通道**。  
本分支选择后者，因为可用 **虚拟 HID** 做设备端，比 SMBus 从设备现实得多。

## SourceMode

配置 / UI 选择：`aura-addressable-sim`

1. **优先** 从命名管道 `\\.\pipe\RgbFxAuraAddressable` 读 65 字节 Host 报文（供未来 VHF 驱动注入）。  
2. **管道不可用时**：用协议栈注入演示用 `SetColors` 报文（仍走解析器），用于测通 → MSI `/api/v1/frame`。  
   - 演示路径 **不等于** 奥创已枚举到设备。

## 其它 Source（并存）

见 [SOURCES.md](SOURCES.md)。`simulator` / `pipe` / `auto` **不会**被本功能删除。

## L3 / L4（后续）

- L3：VHF 虚拟 HID 枚举为 `VID 0B05` + 可配置 PID，把 Host 输出写入上述管道。  
- L4：真机奥创 / OpenRGB 是否识别 — 需测试签名与 PID 实验；成功与否写入本文件或单独 spike 报告。

## 风险

- 奥创可能校验 PID / 固件字符串白名单。  
- 勿与主板上已有 `0B05:18F3` 等真控制器冲突；虚拟 PID 可配置。  
- 禁止向真实 SPD/SMBus 乱写。
