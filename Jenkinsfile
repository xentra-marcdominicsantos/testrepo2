pipeline {
    agent any

    environment {
        SONAR_SERVER = 'SonarQubeServer'          // SonarQube server name in Jenkins
        TEAMS_WEBHOOK = credentials('teams-webhook')
        SSH_CRED_ID = 'jenkins-test-server-ssh'  // SSH credential for test server
        REMOTE_BASE = '/opt/microservices'       // Deployment base path
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

        stage('Discover Microservices') {
            steps {
                script {
                    // Ignore root-level .csproj
                    def services = sh(
                        script: "find . -mindepth 2 -name '*.csproj' | sed 's|./||' | awk -F/ '{print \$1}' | sort -u",
                        returnStdout: true
                    ).trim().split("\n")
                    echo "Detected services: ${services}"
                    // Save for later stages
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
                            withSonarQubeEnv("${SONAR_SERVER}") {
                                sh """
                                    dotnet sonarscanner begin /k:"${svc}" /d:sonar.login="${env.SONAR_AUTH_TOKEN}"
                                    dotnet build
                                    dotnet sonarscanner end /d:sonar.login="${env.SONAR_AUTH_TOKEN}"
                                """
                            }
                        }
                    }
                }
            }
        }

        stage('Deploy to Test Server') {
            when {
                expression { params.ENV == 'test' } // only deploy for test environment
            }
            steps {
                script {
                    def services = env.SERVICES.split(',')
                    def services_to_deploy = (params.SERVICE == 'all') ? services : [params.SERVICE]

                    sshagent([env.SSH_CRED_ID]) {
                        services_to_deploy.each { svc ->
                            def remotePath = "${REMOTE_BASE}/${svc}/${env.BUILD_NUMBER}"

                            // Create folder on test server
                            sh """
                                ssh -o StrictHostKeyChecking=no jenkins@${TEST_SERVER_IP} "mkdir -p ${remotePath}"
                            """

                            // Copy published files
                            sh """
                                scp -o StrictHostKeyChecking=no -r ${svc}/output/* jenkins@${TEST_SERVER_IP}:${remotePath}/
                            """

                            // Create systemd service and restart
                            sh """
                                ssh -o StrictHostKeyChecking=no jenkins@${TEST_SERVER_IP} 'sudo bash -c "
                                UNIT_FILE=/etc/systemd/system/${svc}.service
                                cat > \$UNIT_FILE <<EOL
[Unit]
Description=${svc} .NET Service
After=network.target

[Service]
WorkingDirectory=${remotePath}
ExecStart=/usr/bin/dotnet ${remotePath}/${svc}.dll
Restart=always
RestartSec=5
SyslogIdentifier=${svc}
User=jenkins
Environment=ASPNETCORE_ENVIRONMENT=${params.ENV}

[Install]
WantedBy=multi-user.target
EOL
                                systemctl daemon-reload
                                systemctl enable ${svc}.service
                                systemctl restart ${svc}.service
                                systemctl status ${svc}.service --no-pager || true
                                '"
                            """
                            echo "${svc} deployed successfully."
                        }
                    }
                }
            }
        }

    }

    post {
        always {
            office365ConnectorSend webhookUrl: TEAMS_WEBHOOK,
                message: "Jenkins pipeline completed for Build: ${BUILD_NUMBER} on ENV=${params.ENV}"
        }
        failure {
            office365ConnectorSend webhookUrl: TEAMS_WEBHOOK,
                message: "❌ Jenkins pipeline FAILED for Build: ${BUILD_NUMBER}"
        }
        success {
            office365ConnectorSend webhookUrl: TEAMS_WEBHOOK,
                message: "✅ Jenkins pipeline SUCCESS for Build: ${BUILD_NUMBER}"
        }
    }
}
