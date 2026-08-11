# syntax=docker/dockerfile:1

# Stage 1: Build the static site with Node.
FROM node:lts-alpine AS build
ENV FORCE_COLOR=0
RUN corepack enable
WORKDIR /opt/docusaurus
COPY . /opt/docusaurus/
RUN npm ci
RUN npm run build

# Stage 2: Serve the static site with Nginx.
FROM nginx:alpine AS serve
COPY --from=build /opt/docusaurus/build /usr/share/nginx/html
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
