# RKLLM

基于 RKLLM Runtime 和 .NET 10 Minimal API 的本地推理服务，封装了 OpenAI 兼容接口（`/v1/*`）和 Ollama 兼容接口（`/api/*`），并包含一个简单的 Angular 前端。

该项目只能运行在 Rockchip RK 平台的 Linux 环境中，例如 `rk3576`、`rk3588`。它依赖 RKLLM Runtime、对应的 NPU 环境和 `.rkllm` 模型文件，不支持普通 `x86_64` 主机，也不支持非 RK 的 ARM 设备。

## 功能

- 聊天推理与流式输出
- OpenAI 风格接口与 Ollama 风格接口
- 基于 `config.json` 的模型与端口配置
- 前端静态文件打包到 `wwwroot`

## 运行前提

运行该项目需要：

- Rockchip RK 平台的 Linux 设备，例如 `rk3576`、`rk3588`
- 对应版本的 RKLLM Runtime 动态库，例如 `librkllmrt.so`
- 已转换好的 `.rkllm` 模型文件
- 如需本地开发或构建：`.NET 10 SDK`、`Node.js` 和 `npm`

> 即使构建过程可以在其他机器中完成，生成的程序实际也只能部署到 RK 平台运行。

---

## 配置说明

根目录下的 `config.json`：

```json
{
  "Rkllm": {
    "ModelPath": "models/Qwen_w4a16.rkllm",
    "Port": 8080,
    "Platform": "rk3576",
    "MaxContextLen": 4096
  }
}
```

字段说明：

| 字段 | 说明 |
| --- | --- |
| `ModelPath` | `.rkllm` 模型文件路径 |
| `Port` | 服务监听端口，默认 `8080` |
| `Platform` | 平台类型，如 `rk3576` / `rk3588` |
| `MaxContextLen` | 最大上下文长度 |

---

## 快速启动

### 1. 准备模型与运行库

请确保：

- 模型文件已放入 `models/`，例如 `models/Qwen_w4a16.rkllm`
- RKLLM 原生依赖可被程序找到，例如放在系统库路径或随程序一起部署

### 2. 启动后端

以下命令应在 RK 设备上执行：

```bash
dotnet run --project RKLLM.csproj
```

服务默认地址：

```text
http://127.0.0.1:8080
```

健康检查：

```bash
curl http://127.0.0.1:8080/health
```

---

## 前端开发

前端位于 `ui/`。开发时会通过代理将 `/api/*` 转发到本地后端 `http://127.0.0.1:8080`。

```bash
cd ui
npm install
npm start
```

启动后访问：

```text
http://localhost:4200
```

---

## API 示例

### Ollama 兼容接口

#### 聊天

```bash
curl http://127.0.0.1:8080/api/chat \
  -H "Content-Type: application/json" \
  -d '{
    "model": "Qwen_w4a16",
    "stream": false,
    "messages": [
      { "role": "user", "content": "写一个 Bash 脚本，找出当前目录下最大的 10 个文件。" }
    ]
  }'
```

#### 生成

```bash
curl http://127.0.0.1:8080/api/generate \
  -H "Content-Type: application/json" \
  -d '{
    "model": "Qwen_w4a16",
    "stream": false,
    "prompt": "用 Python 写一个函数，读取 JSON 文件并统计每个顶层键出现的次数。"
  }'
```

### OpenAI 兼容接口

#### Chat Completions

```bash
curl http://127.0.0.1:8080/v1/chat/completions \
  -H "Content-Type: application/json" \
  -d '{
    "model": "Qwen_w4a16",
    "stream": true,
    "messages": [
      { "role": "user", "content": "写一个 Bash 脚本，检查 8080 端口是否被占用，并输出对应进程信息。" }
    ]
  }'
```

---

## 构建与导出

可以使用项目内的脚本完成前后端打包：

```bash
chmod +x scripts/build.sh
./scripts/build.sh
```

默认流程：

1. 构建 Angular UI
2. 执行 `dotnet publish`
3. 将前端产物复制到 `publish/wwwroot`
4. 在 `dist/` 下生成压缩包

默认输出示例：

```text
dist/rkllm-linux-arm64.tar.gz
```

也可以通过环境变量覆盖目标运行时：

```bash
CONFIGURATION=Release RUNTIME=linux-arm64 ./scripts/build.sh
```

### 使用 Docker 导出编译产物

根目录提供了一个基于 `ubuntu:22.04` 的 `Dockerfile`。它会在容器内安装 `.NET 10 SDK`、`Node.js` 和构建依赖，然后直接执行 `scripts/build.sh`。

```bash
docker buildx build \
  --platform linux/arm64 \
  --build-arg RUNTIME=linux-arm64 \
  --output type=local,dest=./docker-out \
  .
```

导出后可在 `docker-out/` 中看到：

- `rkllm-linux-arm64/`：发布目录
- `rkllm-linux-arm64.tar.gz`：压缩包

> 如果是在非 ARM 主机上构建 `linux-arm64`，需要可用的 `buildx` / QEMU 环境。
>
> 生成的程序仍然只能部署到 RK 平台运行。当前 `.dockerignore` 默认排除了 `models/` 和 `libs/`，如需随包部署请自行复制。

---

## 已实现接口

### 基础接口

- `GET /health`

### Ollama 风格

- `GET /api/version`
- `GET /api/tags`
- `GET /api/ps`
- `POST /api/show`
- `POST /api/generate`
- `POST /api/chat`

### OpenAI 风格

- `POST /v1/chat/completions`

---

## TODO

- Embedding 相关接口暂时移除

---

## 注意事项

- 当前服务一次只处理一个请求；并发请求在模型忙时会返回 `503 Server busy`
- `config.json` 中的相对模型路径会自动解析为项目目录或发布目录下的实际路径
- 如果前端未构建，后端仍可单独作为 API 服务运行
- 运行环境必须是 RK 平台；仅完成编译并不表示可以在其他平台直接运行
