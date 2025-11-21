pipeline {
    agent any

    environment {
        SONAR_SERVER = 'SonarQubeServer'  // name of SonarQube server in Jenkins
        TEAMS_WEBHOOK = credentials('teams-webhook')
        SSH_CRED_ID = 'jenkins-test-server-ssh'   // your SSH credential
        REMOTE_BASE = '/opt/microservices'
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
                    services = sh(
                        script: "find . -name '*.csproj' | sed 's|./||' | awk -F/ '{print \$1}' | sort -u",
                        returnStdout: true
                    ).trim().split("\n")
                    echo "Detected services: ${services}"
                }
            }
        }

        stage('Build & Test (parallel)') {
            steps {
                script {
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
            steps {
                script {
                    def services_to_deploy = (params.SERVICE == 'all') ? services : [params.SERVICE]
                    echo "Deploying ${services_to_deploy} to test server ${params.TEST_SERVER_HOST ?: '172.31.7.79'}"

                    sshagent([env.SSH_CRED_ID]) {
                        services_to_deploy.each { svc ->
                            def remotePath = "${REMOTE_BASE}/${svc}/${env.BUILD_NUMBER}"

                            // create folder on test server
                            sh """
                                ssh -o StrictHostKeyChecking=no jenkins@172.31.7.79 "mkdir -p ${remotePath}"
                            """

                            // copy published files
                            sh """
                                scp -o StrictHostKeyChecking=no -r ${svc}/output/* jenkins@172.31.7.79:${remotePath}/
                            """

                            // create systemd unit and restart service
                            sh """
                                ssh -o StrictHostKeyChecking=no jenkins@172.31.7.79 'sudo bash -c "
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
                            echo "${svc} deployed and restarted successfully."
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
