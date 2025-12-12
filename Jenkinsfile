pipeline {
    agent any
        
    environment {
        PATH = "/home/jenkins/.dotnet/tools:${env.PATH}"         
        DOTNET_ROOT = "/usr/lib/dotnet"                            
        SONAR_SERVER = 'SonarQubeServer'
        SSH_CRED_ID = 'jenkins-test-server-ssh'
        REMOTE_BASE = '/opt/microservices'
        TEST_SERVER_IP = '172.31.7.79'

        // NEW
        S3_BUCKET = 'jenkins.legalsynq.com'
        AWS_CRED = 'aws-jenkins-creds'
        BUILD_TIMESTAMP = ""
    }

    parameters {
        choice(name: 'ENV', choices: ['test'], description: 'Environment to deploy')
        choice(name: 'SERVICE', choices: ['all', 'service-a', 'service-b', 'service-c'], description: 'Choose microservice to deploy')
    }

    stages {

        stage('Checkout') {
            steps {
                git branch: 'main', url: 'https://github.com/xentra-marcdominicsantos/testrepo2.git'
            }
        }

        // NEW STAGE
        stage('Generate Timestamp') {
            steps {
                script {
                    env.BUILD_TIMESTAMP = sh(
                        script: "date +%Y-%m-%d_%H-%M-%S",
                        returnStdout: true
                    ).trim()
                    echo "Timestamp: ${env.BUILD_TIMESTAMP}"
                }
            }
        }

        stage('Verify .NET Version') {
            steps {
                sh 'dotnet --version'
            }
        }

        stage('Discover Microservices') {
            steps {
                script {
                    def services = sh(
                        script: "find . -mindepth 2 -name '*.csproj' | sed 's|./||' | awk -F/ '{print \$1}' | sort -u",
                        returnStdout: true
                    ).trim().split("\n")
                    echo "Detected services: ${services}"
                    env.SERVICES = services.join(',')
                }
            }
        }

        stage('Build & Test (parallel)') {
            steps {
                script {
                    def services = env.SERVICES.split(',')
                    def branches = services.collectEntries { svc ->
                        ["${svc}": {
                            stage("Build & Test: ${svc}") {
                                dir(svc) {
                                    sh 'rm -rf output/'
                                    sh "dotnet restore"
                                    sh "dotnet build -c Release"
                                    sh "dotnet test --no-build"
                                    sh "dotnet publish -c Release -o output/"
                                }
                            }
                        }]
                    }
                    parallel branches
                }
            }
        }

        stage('SonarQube Scan') {
            steps {
                script {
                    def services = env.SERVICES.split(',')
                    services.each { svc ->
                        dir(svc) {
                            withCredentials([string(credentialsId: 'sonar_token', variable: 'SONAR_AUTH_TOKEN')]) {
                                sh "dotnet-sonarscanner begin /k:${svc} /d:sonar.login=$SONAR_AUTH_TOKEN /d:sonar.host.url=http://172.31.1.74:9000"
                                sh "dotnet build"
                                sh "dotnet-sonarscanner end /d:sonar.login=$SONAR_AUTH_TOKEN"
                            }
                        }
                    }
                }
            }
        }

        // NEW S3 UPLOAD STAGE
        stage('Upload Build Artifacts to S3') {
            steps {
                script {
                    def services = env.SERVICES.split(',')
                    def services_to_upload = (params.SERVICE == 'all') ? services : [params.SERVICE]

                    withAWS(credentials: "${AWS_CRED}", region: 'us-east-2') {
                        services_to_upload.each { svc ->
                            echo "Uploading ${svc} build to S3 with timestamp ${env.BUILD_TIMESTAMP}..."

                            sh """
                                aws s3 cp ${svc}/output/ \
                                s3://${S3_BUCKET}/JenkinsTestServer/${svc}/${env.BUILD_TIMESTAMP}/ \
                                --recursive
                            """
                        }
                    }
                }
            }
        }

        stage('Cleanup old builds on Test Server') {
            when { expression { params.ENV == 'test' } }
            steps {
                script {
                    def services = env.SERVICES.split(',')
                    def services_to_deploy = (params.SERVICE == 'all') ? services : [params.SERVICE]
                        
                    sshagent([env.SSH_CRED_ID]) {
                        services_to_deploy.each { svc ->
                            sh """
                                ssh -o StrictHostKeyChecking=no jenkins@${TEST_SERVER_IP} '
                                    cd ${REMOTE_BASE}/${svc} &&
                                    ls -1dt */ | tail -n +2 | xargs -r rm -rf
                                '
                            """
                        }
                    }
                }
            }
        }

        stage('Deploy to Test Server') {
            when { expression { params.ENV == 'test' } }
            steps {
                script {
                    def servicePorts = [
                        "service-a": 5000,
                        "service-b": 5001,
                        "service-c": 5002
                    ]

                    def services = env.SERVICES.split(',')
                    def services_to_deploy = (params.SERVICE == 'all') ? services : [params.SERVICE]

                    sshagent([env.SSH_CRED_ID]) {
                        services_to_deploy.each { svc ->

                            def remotePath = "${REMOTE_BASE}/${svc}/${env.BUILD_TIMESTAMP}"
                            def port = servicePorts[svc]

                            sh """
                                ssh -o StrictHostKeyChecking=no jenkins@${TEST_SERVER_IP} "mkdir -p ${remotePath}"
                            """

                            sh """
                                scp -o StrictHostKeyChecking=no -r ${svc}/output/* jenkins@${TEST_SERVER_IP}:${remotePath}/
                            """

                            sh """
ssh -o StrictHostKeyChecking=no jenkins@${TEST_SERVER_IP} "
sudo tee /etc/systemd/system/${svc}.service >/dev/null << EOF
[Unit]
Description=${svc} .NET Service
After=network.target

[Service]
WorkingDirectory=${remotePath}
ExecStart=/usr/lib/dotnet/dotnet ${remotePath}/${svc}.dll
Restart=always
RestartSec=5
SyslogIdentifier=${svc}
User=jenkins
Environment=ASPNETCORE_ENVIRONMENT=${params.ENV}
Environment=ASPNETCORE_URLS=http://0.0.0.0:${port}

[Install]
WantedBy=multi-user.target
EOF

sudo systemctl daemon-reload
sudo systemctl enable ${svc}.service
sudo systemctl restart ${svc}.service
sudo systemctl status ${svc}.service --no-pager || true
"
                            """

                            echo "${svc} deployed successfully on port ${port}."
                        }
                    }
                }
            }
        }
    }

    post {
        always {
            withCredentials([string(credentialsId: 'teams-webhook', variable: 'WEBHOOK')]) {
                office365ConnectorSend webhookUrl: "${WEBHOOK}",
                    message: "Jenkins pipeline completed (Timestamp: ${env.BUILD_TIMESTAMP}) on ENV=${params.ENV}"
            }
        }
        failure {
            withCredentials([string(credentialsId: 'teams-webhook', variable: 'WEBHOOK')]) {
                office365ConnectorSend webhookUrl: "${WEBHOOK}",
                    message: "❌ Jenkins pipeline FAILED (Timestamp: ${env.BUILD_TIMESTAMP})"
            }
        }
        success {
            withCredentials([string(credentialsId: 'teams-webhook', variable: 'WEBHOOK')]) {
                office365ConnectorSend webhookUrl: "${WEBHOOK}",
                    message: "✅ Jenkins pipeline SUCCESS (Timestamp: ${env.BUILD_TIMESTAMP})"
            }
        }
    }
}
