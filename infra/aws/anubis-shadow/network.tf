resource "aws_vpc" "this" {
  cidr_block           = var.vpc_cidr
  enable_dns_hostnames = true
  enable_dns_support   = true

  tags = {
    Name = "${local.name_prefix}-vpc"
  }
}

resource "aws_internet_gateway" "this" {
  vpc_id = aws_vpc.this.id

  tags = {
    Name = "${local.name_prefix}-igw"
  }
}

resource "aws_subnet" "public" {
  vpc_id                  = aws_vpc.this.id
  cidr_block              = var.public_subnet_cidr
  availability_zone       = local.selected_az
  map_public_ip_on_launch = false

  tags = {
    Name = "${local.name_prefix}-public-nat"
  }
}

resource "aws_subnet" "private" {
  vpc_id                  = aws_vpc.this.id
  cidr_block              = var.private_subnet_cidr
  availability_zone       = local.selected_az
  map_public_ip_on_launch = false

  tags = {
    Name = "${local.name_prefix}-private-recorder"
  }
}

resource "aws_eip" "nat" {
  count  = var.enable_nat_gateway ? 1 : 0
  domain = "vpc"

  tags = {
    Name = "${local.name_prefix}-broker-egress"
  }
}

resource "aws_nat_gateway" "this" {
  count         = var.enable_nat_gateway ? 1 : 0
  allocation_id = aws_eip.nat[0].id
  subnet_id     = aws_subnet.public.id

  tags = {
    Name = "${local.name_prefix}-nat"
  }

  depends_on = [aws_internet_gateway.this]
}

resource "aws_route_table" "public" {
  vpc_id = aws_vpc.this.id

  route {
    cidr_block = "0.0.0.0/0"
    gateway_id = aws_internet_gateway.this.id
  }

  tags = {
    Name = "${local.name_prefix}-public"
  }
}

resource "aws_route_table_association" "public" {
  subnet_id      = aws_subnet.public.id
  route_table_id = aws_route_table.public.id
}

resource "aws_route_table" "private" {
  vpc_id = aws_vpc.this.id

  dynamic "route" {
    for_each = var.enable_nat_gateway ? [1] : []
    content {
      cidr_block     = "0.0.0.0/0"
      nat_gateway_id = aws_nat_gateway.this[0].id
    }
  }

  tags = {
    Name = "${local.name_prefix}-private"
  }
}

resource "aws_route_table_association" "private" {
  subnet_id      = aws_subnet.private.id
  route_table_id = aws_route_table.private.id
}

resource "aws_security_group" "recorder" {
  name        = "${local.name_prefix}-recorder"
  description = "AWS1 recorder host: no ingress, explicit market-data and AWS service egress only"
  vpc_id      = aws_vpc.this.id

  tags = {
    Name = "${local.name_prefix}-recorder"
  }
}

resource "aws_vpc_security_group_egress_rule" "broker_market_data" {
  for_each          = toset(var.lmax_market_data_egress_cidrs)
  security_group_id = aws_security_group.recorder.id
  ip_protocol       = "tcp"
  from_port         = var.lmax_market_data_port
  to_port           = var.lmax_market_data_port
  cidr_ipv4         = each.value
  description       = "Explicit LMAX market-data-only egress; DNS-resolved non-contractual /32"
}

resource "aws_vpc_security_group_egress_rule" "aws_private_endpoints" {
  count             = var.enable_private_endpoints ? 1 : 0
  security_group_id = aws_security_group.recorder.id
  ip_protocol       = "tcp"
  from_port         = 443
  to_port           = 443
  cidr_ipv4         = var.vpc_cidr
  description       = "AWS private endpoint HTTPS egress inside VPC"
}

resource "aws_security_group" "endpoints" {
  count       = var.enable_private_endpoints ? 1 : 0
  name        = "${local.name_prefix}-endpoints"
  description = "Interface endpoint access from recorder only"
  vpc_id      = aws_vpc.this.id

  tags = {
    Name = "${local.name_prefix}-endpoints"
  }
}

resource "aws_vpc_security_group_ingress_rule" "endpoint_https_from_recorder" {
  count                        = var.enable_private_endpoints ? 1 : 0
  security_group_id            = aws_security_group.endpoints[0].id
  referenced_security_group_id = aws_security_group.recorder.id
  ip_protocol                  = "tcp"
  from_port                    = 443
  to_port                      = 443
  description                  = "Recorder to AWS interface endpoints"
}

resource "aws_vpc_security_group_egress_rule" "endpoint_response" {
  count             = var.enable_private_endpoints ? 1 : 0
  security_group_id = aws_security_group.endpoints[0].id
  ip_protocol       = "-1"
  cidr_ipv4         = var.vpc_cidr
  description       = "Endpoint response traffic inside VPC"
}

locals {
  interface_endpoint_services = toset([
    "ssm",
    "ssmmessages",
    "ec2messages",
    "logs",
    "monitoring",
    "secretsmanager",
    "kms"
  ])
}

resource "aws_vpc_endpoint" "interface" {
  for_each            = var.enable_private_endpoints ? local.interface_endpoint_services : toset([])
  vpc_id              = aws_vpc.this.id
  service_name        = "com.amazonaws.${var.region}.${each.value}"
  vpc_endpoint_type   = "Interface"
  private_dns_enabled = true
  subnet_ids          = [aws_subnet.private.id]
  security_group_ids  = [aws_security_group.endpoints[0].id]

  tags = {
    Name = "${local.name_prefix}-${each.value}"
  }
}

resource "aws_vpc_endpoint" "s3" {
  count             = var.enable_private_endpoints ? 1 : 0
  vpc_id            = aws_vpc.this.id
  service_name      = "com.amazonaws.${var.region}.s3"
  vpc_endpoint_type = "Gateway"
  route_table_ids   = [aws_route_table.private.id]

  tags = {
    Name = "${local.name_prefix}-s3"
  }
}
