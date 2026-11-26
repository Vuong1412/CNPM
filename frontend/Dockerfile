FROM node:22-alpine AS build
WORKDIR /workspace
COPY frontend/package.json frontend/package-lock.json ./
COPY frontend/shared/package.json ./shared/package.json
COPY frontend/user-web/package.json ./user-web/package.json
RUN npm ci
COPY frontend/shared ./shared
COPY frontend/user-web ./user-web
ARG VITE_API_BASE_URL=/api/
ENV VITE_API_BASE_URL=${VITE_API_BASE_URL}
RUN npx tsc -b shared --force && npm run build --workspace @qol/user-web
FROM nginx:alpine
COPY deployment/nginx/default.conf /etc/nginx/conf.d/default.conf
COPY --from=build /workspace/user-web/dist /usr/share/nginx/html
EXPOSE 80
