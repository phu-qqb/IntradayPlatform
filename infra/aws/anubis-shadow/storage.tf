resource "aws_s3_bucket" "archive" {
  bucket        = local.archive_bucket_name
  force_destroy = false

  lifecycle {
    prevent_destroy = true
  }

  tags = {
    Name    = local.archive_bucket_name
    Purpose = "m2-capture-artifacts-archives-and-manifests"
  }
}

resource "aws_s3_bucket_public_access_block" "archive" {
  bucket                  = aws_s3_bucket.archive.id
  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

resource "aws_s3_bucket_versioning" "archive" {
  bucket = aws_s3_bucket.archive.id

  versioning_configuration {
    status = "Enabled"
  }
}

resource "aws_s3_bucket_server_side_encryption_configuration" "archive" {
  bucket = aws_s3_bucket.archive.id

  rule {
    apply_server_side_encryption_by_default {
      sse_algorithm = "AES256"
    }
  }
}

resource "aws_s3_bucket_lifecycle_configuration" "archive" {
  bucket = aws_s3_bucket.archive.id

  rule {
    id     = "retain-versioned-recorder-archives"
    status = "Enabled"

    filter {
      prefix = ""
    }

    abort_incomplete_multipart_upload {
      days_after_initiation = 7
    }

    noncurrent_version_transition {
      noncurrent_days = 30
      storage_class   = "STANDARD_IA"
    }
  }
}

data "aws_iam_policy_document" "archive_bucket" {
  statement {
    sid    = "DenyInsecureTransport"
    effect = "Deny"

    principals {
      type        = "*"
      identifiers = ["*"]
    }

    actions = ["s3:*"]
    resources = [
      aws_s3_bucket.archive.arn,
      "${aws_s3_bucket.archive.arn}/*"
    ]

    condition {
      test     = "Bool"
      variable = "aws:SecureTransport"
      values   = ["false"]
    }
  }
}

resource "aws_s3_bucket_policy" "archive" {
  bucket = aws_s3_bucket.archive.id
  policy = data.aws_iam_policy_document.archive_bucket.json
}

resource "aws_secretsmanager_secret" "market_data_only" {
  count                   = var.credential_secret_arn == null ? 1 : 0
  name                    = var.credential_secret_name
  description             = "Anubis AWS1 LMAX market-data-only credential envelope. Terraform creates metadata only; value is populated out of band."
  recovery_window_in_days = 7

  tags = {
    CredentialScope = "MARKET_DATA_ONLY"
    ContainsOrders  = "false"
  }
}

resource "aws_ssm_parameter" "endpoint_alias" {
  name        = "/anubis/${local.safe_environment}/aws1/lmax/market-data-endpoint-alias"
  description = "LMAX market-data endpoint alias only; no host, account, or secret value."
  type        = "String"
  value       = var.lmax_market_data_endpoint_alias
}
