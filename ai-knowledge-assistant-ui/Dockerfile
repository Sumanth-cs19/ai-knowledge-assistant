FROM node:24-alpine AS build
WORKDIR /app
COPY ai-knowledge-assistant-ui/package*.json ./
RUN npm ci
COPY ai-knowledge-assistant-ui/ ./
RUN npm run build

FROM nginx:1.27-alpine
COPY ai-knowledge-assistant-ui/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist/ai-knowledge-assistant-ui/browser /usr/share/nginx/html
EXPOSE 80
HEALTHCHECK --interval=30s --timeout=5s --retries=3 CMD wget -qO- http://localhost/ || exit 1
