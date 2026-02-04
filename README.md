# PostGisTools (WPF)

## 功能
- **数据库连接配置**：主机/端口/数据库/用户/密码；可测试连接。
- **架构/表/字段浏览**：树形展示 Schema -> Table -> Columns。
- **字段配置**：可配置字段显示、别名、类型/长度/默认值（本地元数据，不修改数据库）。
- **批量添加字段**：在架构级别批量为该架构内所有表新增字段（已存在则跳过）。
- **坐标系转换**：在表级别选择目标坐标系并转换 geometry/geography 字段。
- **数据 CRUD**：表数据加载（默认限制 200 行）、新增/删除/编辑、保存变更。

## 运行环境
- Windows 桌面
- .NET 8 (WPF)

## 使用步骤
1. 启动应用，进入 **Connection** 页签。
2. 填写 Host/Port/Database/User/Password，点击 **Test Connection**。
   - 连接成功后，连接信息（不含密码）会保存到本地配置。
3. 进入 **Schema** 页签，点击 **REFRESH SCHEMA** 加载架构。
4. 选择表后在右侧字段表格中编辑本地字段配置，点击 **SAVE FIELD CONFIG** 进行保存。
5. 进入 **Data** 页签：
   - 选择 Schema / Table
   - 点击 **Load Data** 加载数据（限制 200 行）
   - 支持新增/删除/编辑行并 **Save Changes** 提交

## 本地配置
配置文件存储在：
```
%APPDATA%\PostGisTools\config.json
```

说明：
- **连接信息**：仅保存 Host/Port/Database/Username；不保存密码。
- **字段配置**：保存每个字段的显示/别名/本地类型/长度/默认值。

## 注意事项
- 若表无主键：**Data** 页的新增/删除/保存会被禁用（只读）。
- 几何字段（geometry/geography）不会加载进数据表。
- 坐标系转换依赖 PostGIS 的 `geometry_columns`/`geography_columns` 与 `ST_Transform`。
