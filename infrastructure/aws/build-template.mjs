// Generates plain CloudFormation JSON without an infrastructure framework dependency.
// Run: node infrastructure/aws/build-template.mjs
import { writeFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
const ref = name => ({ Ref: name })
const att = (name, key) => ({ 'Fn::GetAtt': [name, key] })
const sub = text => ({ 'Fn::Sub': text })
const resources = {}
const add = (name, type, properties, extra = {}) => { resources[name] = { Type: type, Properties: properties, ...extra }; return ref(name) }
const policy = statements => ({ Version: '2012-10-17', Statement: statements })
const allow = (Action, Resource) => ({ Effect: 'Allow', Action, Resource })
const assume = service => policy([{ Effect: 'Allow', Principal: { Service: service }, Action: 'sts:AssumeRole' }])
const tags = [{ Key: 'Project', Value: 'DevFlow' }]
add('Vpc', 'AWS::EC2::VPC', { CidrBlock: '10.42.0.0/16', EnableDnsSupport: true, EnableDnsHostnames: true, Tags: tags })
add('Gateway', 'AWS::EC2::InternetGateway', { Tags: tags })
add('GatewayAttachment', 'AWS::EC2::VPCGatewayAttachment', { VpcId: ref('Vpc'), InternetGatewayId: ref('Gateway') })
add('PublicRoutes', 'AWS::EC2::RouteTable', { VpcId: ref('Vpc') })
add('InternetRoute', 'AWS::EC2::Route', { RouteTableId: ref('PublicRoutes'), DestinationCidrBlock: '0.0.0.0/0', GatewayId: ref('Gateway') }, { DependsOn: 'GatewayAttachment' })
for (const [index, zone] of ['A', 'B'].entries()) {
  for (const [kind, octet] of [['Public', index], ['Private', index + 10]]) {
    const name = `${kind}${zone}`
    add(name, 'AWS::EC2::Subnet', { VpcId: ref('Vpc'), CidrBlock: `10.42.${octet}.0/24`, AvailabilityZone: { 'Fn::Select': [index, { 'Fn::GetAZs': '' }] }, MapPublicIpOnLaunch: false })
    if (kind === 'Public') add(`${name}Route`, 'AWS::EC2::SubnetRouteTableAssociation', { SubnetId: ref(name), RouteTableId: ref('PublicRoutes') })
  }
}
add('AlbSecurityGroup', 'AWS::EC2::SecurityGroup', { GroupDescription: 'CloudFront origin traffic only', VpcId: ref('Vpc'), SecurityGroupIngress: [{ IpProtocol: 'tcp', FromPort: 80, ToPort: 80, SourcePrefixListId: ref('CloudFrontPrefixListId') }] })
add('TaskSecurityGroup', 'AWS::EC2::SecurityGroup', { GroupDescription: 'Only ALB can reach application', VpcId: ref('Vpc'), SecurityGroupIngress: [{ IpProtocol: 'tcp', FromPort: 8080, ToPort: 8080, SourceSecurityGroupId: ref('AlbSecurityGroup') }] })
add('DatabaseSecurityGroup', 'AWS::EC2::SecurityGroup', { GroupDescription: 'Only application and migration tasks can reach PostgreSQL', VpcId: ref('Vpc'), SecurityGroupIngress: [{ IpProtocol: 'tcp', FromPort: 5432, ToPort: 5432, SourceSecurityGroupId: ref('TaskSecurityGroup') }] })
for (const name of ['AdminPassword', 'AppPassword']) add(name, 'AWS::SecretsManager::Secret', { GenerateSecretString: { PasswordLength: 40, ExcludePunctuation: true }, Tags: tags }, { DeletionPolicy: 'Retain', UpdateReplacePolicy: 'Retain' })
add('DatabaseSubnets', 'AWS::RDS::DBSubnetGroup', { DBSubnetGroupDescription: 'Private DevFlow database subnets', SubnetIds: [ref('PrivateA'), ref('PrivateB')] })
add('DatabaseParameters', 'AWS::RDS::DBParameterGroup', { Description: 'Require PostgreSQL TLS', Family: 'postgres18', Parameters: { 'rds.force_ssl': '1' } })
add('Database', 'AWS::RDS::DBInstance', {
  Engine: 'postgres', EngineVersion: ref('PostgresVersion'), DBInstanceClass: 'db.t4g.micro',
  DBName: 'devflow', MasterUsername: 'devflow_admin', MasterUserPassword: sub('{{resolve:secretsmanager:${AdminPassword}:SecretString}}'),
  AllocatedStorage: '20', StorageType: 'gp3', StorageEncrypted: true, PubliclyAccessible: false,
  MultiAZ: false, BackupRetentionPeriod: 7, DeletionProtection: true, AutoMinorVersionUpgrade: true,
  CopyTagsToSnapshot: true, CACertificateIdentifier: 'rds-ca-rsa2048-g1',
  DBSubnetGroupName: ref('DatabaseSubnets'), DBParameterGroupName: ref('DatabaseParameters'), VPCSecurityGroups: [ref('DatabaseSecurityGroup')], Tags: tags
}, { DeletionPolicy: 'Snapshot', UpdateReplacePolicy: 'Snapshot' })
add('Repository', 'AWS::ECR::Repository', { ImageTagMutability: 'IMMUTABLE', ImageScanningConfiguration: { ScanOnPush: true }, EncryptionConfiguration: { EncryptionType: 'AES256' }, Tags: tags }, { DeletionPolicy: 'Retain', UpdateReplacePolicy: 'Retain' })
add('Logs', 'AWS::Logs::LogGroup', { RetentionInDays: 14, Tags: tags })
add('Cluster', 'AWS::ECS::Cluster', { ClusterSettings: [{ Name: 'containerInsights', Value: 'disabled' }], Tags: tags })
const logPolicy = allow(['logs:CreateLogStream', 'logs:PutLogEvents'], att('Logs', 'Arn'))
const pullPolicies = [allow('ecr:GetAuthorizationToken', '*'), allow(['ecr:BatchCheckLayerAvailability', 'ecr:GetDownloadUrlForLayer', 'ecr:BatchGetImage'], att('Repository', 'Arn')), logPolicy]
for (const [name, secrets] of [['ApiExecutionRole', ['AppPassword']], ['MigrationExecutionRole', ['AppPassword', 'AdminPassword']]]) {
  add(name, 'AWS::IAM::Role', { Path: '/devflow/', AssumeRolePolicyDocument: assume('ecs-tasks.amazonaws.com'), Policies: [{ PolicyName: 'Execution', PolicyDocument: policy([...pullPolicies, allow('secretsmanager:GetSecretValue', secrets.map(ref))]) }] })
}
add('TaskRole', 'AWS::IAM::Role', { Path: '/devflow/', AssumeRolePolicyDocument: assume('ecs-tasks.amazonaws.com'), Policies: [{ PolicyName: 'Attachments', PolicyDocument: policy([
  allow(['s3:GetBucketVersioning', 's3:ListBucket'], sub('arn:${AWS::Partition}:s3:::${AttachmentBucket}')),
  allow(['s3:GetObject', 's3:PutObject', 's3:DeleteObject'], sub('arn:${AWS::Partition}:s3:::${AttachmentBucket}/attachments/*'))
]) }] })
add('MigrationRole', 'AWS::IAM::Role', { Path: '/devflow/', AssumeRolePolicyDocument: assume('ecs-tasks.amazonaws.com') })
const env = (Name, Value) => ({ Name, Value })
const databaseEnvironment = username => [env('Database__Host', att('Database', 'Endpoint.Address')), env('Database__Username', username), env('Database__RootCertificate', '/app/certs/rds.pem')]
const container = migration => ({
  Name: migration ? 'migrate' : 'api', Image: sub('${Repository.RepositoryUri}:${ImageTag}'), Essential: true,
  ...(migration ? { EntryPoint: ['dotnet', '/migration/DevFlow.Migrator.dll'] } : { PortMappings: [{ ContainerPort: 8080, Protocol: 'tcp' }] }),
  Environment: [...databaseEnvironment(migration ? 'devflow_admin' : 'devflow_app'), ...(!migration ? [
    env('AllowedHosts', '*'), env('AWS_REGION', ref('AWS::Region')), env('Attachments__Region', ref('AWS::Region')), env('Attachments__BucketName', ref('AttachmentBucket')),
    env('Cognito__Authority', sub('https://cognito-idp.${AWS::Region}.amazonaws.com/${UserPoolId}')),
    env('Cognito__ClientId', ref('ClientId')), env('Cognito__Domain', ref('CognitoDomain'))
  ] : [])],
  Secrets: [{ Name: 'Database__Password', ValueFrom: ref(migration ? 'AdminPassword' : 'AppPassword') }, ...(migration ? [{ Name: 'Database__ApplicationPassword', ValueFrom: ref('AppPassword') }] : [])],
  LogConfiguration: { LogDriver: 'awslogs', Options: { 'awslogs-group': ref('Logs'), 'awslogs-region': ref('AWS::Region'), 'awslogs-stream-prefix': migration ? 'migration' : 'api' } },
  StopTimeout: 30
})
for (const migration of [false, true]) add(migration ? 'MigrationTask' : 'ApiTask', 'AWS::ECS::TaskDefinition', {
  Cpu: '256', Memory: '512', NetworkMode: 'awsvpc', RequiresCompatibilities: ['FARGATE'],
  RuntimePlatform: { CpuArchitecture: 'X86_64', OperatingSystemFamily: 'LINUX' },
  ExecutionRoleArn: att(migration ? 'MigrationExecutionRole' : 'ApiExecutionRole', 'Arn'),
  TaskRoleArn: att(migration ? 'MigrationRole' : 'TaskRole', 'Arn'), ContainerDefinitions: [container(migration)]
})
add('LoadBalancer', 'AWS::ElasticLoadBalancingV2::LoadBalancer', { Scheme: 'internal', Type: 'application', SecurityGroups: [ref('AlbSecurityGroup')], Subnets: [ref('PrivateA'), ref('PrivateB')], LoadBalancerAttributes: [{ Key: 'idle_timeout.timeout_seconds', Value: '120' }], Tags: tags })
add('Targets', 'AWS::ElasticLoadBalancingV2::TargetGroup', { VpcId: ref('Vpc'), Port: 8080, Protocol: 'HTTP', TargetType: 'ip', HealthCheckPath: '/api/health/ready', HealthCheckIntervalSeconds: 30, HealthyThresholdCount: 2, TargetGroupAttributes: [{ Key: 'deregistration_delay.timeout_seconds', Value: '30' }] })
add('Listener', 'AWS::ElasticLoadBalancingV2::Listener', { LoadBalancerArn: ref('LoadBalancer'), Port: 80, Protocol: 'HTTP', DefaultActions: [{ Type: 'forward', TargetGroupArn: ref('Targets') }] })
add('Service', 'AWS::ECS::Service', {
  Cluster: ref('Cluster'), TaskDefinition: ref('ApiTask'), LaunchType: 'FARGATE', PlatformVersion: '1.4.0', DesiredCount: ref('DesiredCount'),
  DeploymentConfiguration: { MinimumHealthyPercent: 0, MaximumPercent: 100, DeploymentCircuitBreaker: { Enable: true, Rollback: true } },
  HealthCheckGracePeriodSeconds: 120,
  NetworkConfiguration: { AwsvpcConfiguration: { AssignPublicIp: 'ENABLED', Subnets: [ref('PublicA'), ref('PublicB')], SecurityGroups: [ref('TaskSecurityGroup')] } },
  LoadBalancers: [{ ContainerName: 'api', ContainerPort: 8080, TargetGroupArn: ref('Targets') }], Tags: tags
}, { DependsOn: ['Listener', 'InternetRoute', 'PublicARoute', 'PublicBRoute'] })
add('VpcOrigin', 'AWS::CloudFront::VpcOrigin', { VpcOriginEndpointConfig: { Arn: ref('LoadBalancer'), Name: sub('${AWS::StackName}-api'), HTTPPort: 80, HTTPSPort: 443, OriginProtocolPolicy: 'http-only', OriginSSLProtocols: ['TLSv1.2'] } }, { DependsOn: ['GatewayAttachment', 'Listener'] })
add('FrontendBucket', 'AWS::S3::Bucket', { PublicAccessBlockConfiguration: { BlockPublicAcls: true, BlockPublicPolicy: true, IgnorePublicAcls: true, RestrictPublicBuckets: true }, OwnershipControls: { Rules: [{ ObjectOwnership: 'BucketOwnerEnforced' }] }, BucketEncryption: { ServerSideEncryptionConfiguration: [{ ServerSideEncryptionByDefault: { SSEAlgorithm: 'AES256' } }] }, Tags: tags }, { DeletionPolicy: 'Retain', UpdateReplacePolicy: 'Retain' })
add('OriginAccess', 'AWS::CloudFront::OriginAccessControl', { OriginAccessControlConfig: { Name: sub('${AWS::StackName}-frontend'), OriginAccessControlOriginType: 's3', SigningBehavior: 'always', SigningProtocol: 'sigv4' } })
export const spaCode = `function handler(event) {
  var request = event.request;
  var uri = request.uri;
  if (uri !== '/api' && uri.indexOf('/api/') !== 0 && uri.split('/').pop().indexOf('.') === -1) {
    request.uri = '/index.html';
  }
  return request;
}`
add('SpaFunction', 'AWS::CloudFront::Function', { Name: sub('${AWS::StackName}-spa'), AutoPublish: true, FunctionConfig: { Comment: 'Frontend routes only; never rewrite API errors', Runtime: 'cloudfront-js-2.0' }, FunctionCode: spaCode })
add('FrontendCache', 'AWS::CloudFront::CachePolicy', { CachePolicyConfig: {
  Name: sub('${AWS::StackName}-frontend'), MinTTL: 0, DefaultTTL: 0, MaxTTL: 31536000,
  ParametersInCacheKeyAndForwardedToOrigin: { EnableAcceptEncodingGzip: true, EnableAcceptEncodingBrotli: true,
    CookiesConfig: { CookieBehavior: 'none' }, HeadersConfig: { HeaderBehavior: 'none' }, QueryStringsConfig: { QueryStringBehavior: 'none' } }
} })
const apiBehavior = PathPattern => ({ PathPattern, TargetOriginId: 'api', ViewerProtocolPolicy: 'https-only', AllowedMethods: ['GET', 'HEAD', 'OPTIONS', 'PUT', 'POST', 'PATCH', 'DELETE'], CachedMethods: ['GET', 'HEAD'], CachePolicyId: '4135ea2d-6df8-44a3-9df3-4b5a84be39ad', OriginRequestPolicyId: 'b689b0a8-53d0-40ab-baf2-68738e2966ac', Compress: true })
add('Distribution', 'AWS::CloudFront::Distribution', { DistributionConfig: {
  Enabled: true, Comment: 'DevFlow frontend and authenticated API', DefaultRootObject: 'index.html', HttpVersion: 'http2', IPV6Enabled: true, PriceClass: 'PriceClass_100',
  Origins: [
    { Id: 'frontend', DomainName: att('FrontendBucket', 'RegionalDomainName'), OriginAccessControlId: ref('OriginAccess'), S3OriginConfig: { OriginAccessIdentity: '' } },
    { Id: 'api', DomainName: att('LoadBalancer', 'DNSName'), VpcOriginConfig: { VpcOriginId: att('VpcOrigin', 'Id'), OriginReadTimeout: 60 } }
  ],
  DefaultCacheBehavior: { TargetOriginId: 'frontend', ViewerProtocolPolicy: 'redirect-to-https', AllowedMethods: ['GET', 'HEAD'], CachedMethods: ['GET', 'HEAD'], Compress: true,
    CachePolicyId: ref('FrontendCache'), ResponseHeadersPolicyId: '67f7725c-6f97-4210-82d7-5512b31e9d03',
    FunctionAssociations: [{ EventType: 'viewer-request', FunctionARN: att('SpaFunction', 'FunctionARN') }] },
  CacheBehaviors: [apiBehavior('/api'), apiBehavior('/api/*')], ViewerCertificate: { CloudFrontDefaultCertificate: true },
  CustomErrorResponses: [400, 403, 404, 405, 414, 416, 500, 501, 502, 503, 504].map(ErrorCode => ({ ErrorCode, ErrorCachingMinTTL: 0 }))
} })
add('FrontendPolicy', 'AWS::S3::BucketPolicy', { Bucket: ref('FrontendBucket'), PolicyDocument: policy([
  { Effect: 'Allow', Principal: { Service: 'cloudfront.amazonaws.com' }, Action: 's3:GetObject', Resource: sub('${FrontendBucket.Arn}/*'), Condition: { StringEquals: { 'AWS:SourceArn': sub('arn:${AWS::Partition}:cloudfront::${AWS::AccountId}:distribution/${Distribution}') } } },
  { Effect: 'Deny', Principal: '*', Action: 's3:*', Resource: [att('FrontendBucket', 'Arn'), sub('${FrontendBucket.Arn}/*')], Condition: { Bool: { 'aws:SecureTransport': 'false' } } }
]) })
export const template = {
  AWSTemplateFormatVersion: '2010-09-09', Description: 'DevFlow single-instance portfolio deployment. Creates billable resources; initial desired count is zero.',
  Parameters: {
    AttachmentBucket: { Type: 'String', AllowedPattern: '[a-z0-9][a-z0-9-]{1,61}[a-z0-9]' },
    UserPoolId: { Type: 'String', AllowedPattern: 'ca-central-1_[A-Za-z0-9]+' },
    ClientId: { Type: 'String', AllowedPattern: '[a-z0-9]+' },
    CognitoDomain: { Type: 'String', AllowedPattern: 'https://[a-z0-9.-]+' },
    CloudFrontPrefixListId: { Type: 'String', AllowedPattern: 'pl-[a-f0-9]+' },
    PostgresVersion: { Type: 'String', AllowedPattern: '18\\.[0-9]+', Description: 'Verify available RDS PostgreSQL 18 version before deployment.' },
    ImageTag: { Type: 'String', Default: 'bootstrap', AllowedPattern: '[A-Za-z0-9][A-Za-z0-9_.-]{0,127}' },
    DesiredCount: { Type: 'Number', Default: 0, AllowedValues: [0, 1] }
  }, Resources: resources,
  Outputs: Object.fromEntries(Object.entries({
    RepositoryUri: att('Repository', 'RepositoryUri'), Cluster: ref('Cluster'), Service: att('Service', 'Name'),
    MigrationTask: ref('MigrationTask'), ApiTask: ref('ApiTask'), TaskSecurityGroup: ref('TaskSecurityGroup'),
    PublicSubnets: { 'Fn::Join': [',', [ref('PublicA'), ref('PublicB')]] },
    FrontendBucket: ref('FrontendBucket'), DistributionId: ref('Distribution'), SiteUrl: sub('https://${Distribution.DomainName}'),
    Logs: ref('Logs'), DatabaseId: ref('Database')
  }).map(([key, Value]) => [key, { Value }]))
}
if (process.argv[1] === fileURLToPath(import.meta.url)) {
  writeFileSync(new URL('./devflow.template.json', import.meta.url), JSON.stringify(template, null, 2) + '\n')
  console.log(`Generated ${Object.keys(resources).length} CloudFormation resources.`)
}
