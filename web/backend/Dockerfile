FROM python:3.13-slim
WORKDIR /app
COPY requirements.txt ./
RUN pip install --no-cache-dir -r requirements.txt
COPY . .
# Logs appear immediately (Sky reads them when a deployment fails).
ENV PORT=8000 PYTHONUNBUFFERED=1
EXPOSE 8000
CMD ["sh", "-c", "exec python -m uvicorn main:app --host 0.0.0.0 --port ${PORT:-8000}"]
