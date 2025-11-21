pipeline {
    agent any

    environment {
        PATH = "/snap/bin:/home/jenkins/.dotnet/tools:${env.PATH}" // Include Snap binaries + dotnet global tools
        DOTNET_ROOT = "/snap/dotnet-sdk/current"                  // .NET root
        SONAR_SERVER = 'SonarQubeServer'                          // SonarQube server name in Jenkins
        SSH_CRED_ID = 'jenkins-test-server-ssh'                  // SSH credential for test server
        REMOTE_BASE = '/opt/microservices'                       // Deployment base path
        TEST_SERVER_IP = '172.31.7.79'
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
                                    // Clean output folder before publish
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

        stage('Deploy to Test Server') {
            when { expression { params.ENV == 'test' } }
            steps {
                script {
                    // Assign unique ports for each service
                    def servicePorts = [
                        "service-a": 5000,
                        "service-b": 5001,
                        "service-c": 5002
                    ]

                    def services = env.SERVICES.split(',')
                    def services_to_deploy = (params.SERVICE == 'all') ? services : [params.SERVICE]

                    sshagent([env.SSH_CRED_ID]) {
                        services_to_deploy.each { svc ->
                            def remotePath = "${REMOTE_BASE}/${svc}/${env.BUILD_NUMBER}"
                            def port = servicePorts[svc]

                            // Create folder on test server
                            sh """
                                ssh -o StrictHostKeyChecking=no jenkins@${TEST_SERVER_IP} "mkdir -p ${remotePath}"
                            """

                            // Copy published files
                            sh """
                                scp -o StrictHostKeyChecking=no -r ${svc}/output/* jenkins@${TEST_SERVER_IP}:${remotePath}/
                            """

                            // Create systemd service with unique port
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
                    message: "Jenkins pipeline completed for Build: ${BUILD_NUMBER} on ENV=${params.ENV}"
            }
        }
        failure {
            withCredentials([string(credentialsId: 'teams-webhook', variable: 'WEBHOOK')]) {
                office365ConnectorSend webhookUrl: "${WEBHOOK}",
                    message: "❌ Jenkins pipeline FAILED for Build: ${BUILD_NUMBER}"
            }
        }
        success {
            withCredentials([string(credentialsId: 'teams-webhook', variable: 'WEBHOOK')]) {
                office365ConnectorSend webhookUrl: "${WEBHOOK}",
                    message: "✅ Jenkins pipeline SUCCESS for Build: ${BUILD_NUMBER}"
            }
        }
    }
}
