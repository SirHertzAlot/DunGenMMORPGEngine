{pkgs}: {
  deps = [
    pkgs.nodePackages_latest.graphql
    pkgs.helm
    pkgs.prometheus
    pkgs.grafana
    pkgs.redis
    pkgs.mongodb-5_0
  ];
}
