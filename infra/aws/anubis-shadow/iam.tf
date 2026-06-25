resource "aws_iam_role" "recorder" {
  name = "${local.name_prefix}-recorder"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Principal = {
          Service = "ec2.amazonaws.com"
        }
        Action = "sts:AssumeRole"
      }
    ]
  })
}

resource "aws_iam_instance_profile" "recorder" {
  name = "${local.name_prefix}-recorder"
  role = aws_iam_role.recorder.name
}

resource "aws_iam_role_policy_attachment" "ssm_core" {
  role       = aws_iam_role.recorder.name
  policy_arn = "arn:aws:iam::aws:policy/AmazonSSMManagedInstanceCore"
}

resource "aws_iam_policy" "recorder" {
  name        = "${local.name_prefix}-recorder"
  description = "Least-privilege recorder policy for market-data-only capture, S3 archive, CloudWatch, and Secrets Manager read."

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid    = "ArchiveBucketList"
        Effect = "Allow"
        Action = [
          "s3:ListBucket"
        ]
        Resource = aws_s3_bucket.archive.arn
      },
      {
        Sid    = "ArchiveObjectWriteRead"
        Effect = "Allow"
        Action = [
          "s3:GetObject",
          "s3:PutObject",
          "s3:PutObjectTagging",
          "s3:AbortMultipartUpload"
        ]
        Resource = "${aws_s3_bucket.archive.arn}/*"
      },
      {
        Sid    = "ReadMarketDataOnlySecret"
        Effect = "Allow"
        Action = [
          "secretsmanager:DescribeSecret",
          "secretsmanager:GetSecretValue"
        ]
        Resource = local.credential_secret_arn
      },
      {
        Sid    = "ReadEndpointAliasParameter"
        Effect = "Allow"
        Action = [
          "ssm:GetParameter"
        ]
        Resource = aws_ssm_parameter.endpoint_alias.arn
      },
      {
        Sid    = "WriteRecorderLogs"
        Effect = "Allow"
        Action = [
          "logs:CreateLogStream",
          "logs:DescribeLogStreams",
          "logs:PutLogEvents"
        ]
        Resource = [
          "${aws_cloudwatch_log_group.recorder.arn}:*",
          "${aws_cloudwatch_log_group.install.arn}:*"
        ]
      },
      {
        Sid    = "WriteRecorderMetrics"
        Effect = "Allow"
        Action = "cloudwatch:PutMetricData"
        Resource = "*"
        Condition = {
          StringEquals = {
            "cloudwatch:namespace" = var.cloudwatch_namespace
          }
        }
      }
    ]
  })
}

resource "aws_iam_role_policy_attachment" "recorder" {
  role       = aws_iam_role.recorder.name
  policy_arn = aws_iam_policy.recorder.arn
}
