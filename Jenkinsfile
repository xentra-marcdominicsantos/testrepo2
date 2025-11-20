pipeline {
    agent any
    environment {
        SONAR_SERVER = 'http://sonarqube.legalsynq.com:9000'
        TEAMS_WEBHOOK = credentials('teams-webhook')
    }

    parameters {
        choice(name: 'ENV', choices: ['test'], description: 'Choose environment to deploy')
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
                    services = sh(script: "find . -name '*.csproj' | sed 's|./||' | awk -F/ '{print \$1}' | sort -u", returnStdout: true).trim().split("\n")
                    echo "Detected services: ${services}"
                }
            }
        }

        stage('Build & Test') {
    steps {
        script {
            services.each { svc ->
                echo "Building and testing ${svc}"
                dir(svc) {
                    sh "dotnet restore"
                    sh "dotnet build -c Release"
                    sh "dotnet test --no-build"
                                    }
                                }
                            }
                        }
                    }
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

        stage('Publish Artifacts') {
            steps {
                script {
                    services.each { svc ->
                        dir(svc) {
                            sh "dotnet publish -c Release -o output/"
                        }
                    }
                }
            }
        }

        stage('Deploy to Test Server') {
            steps {
                script {
                    def server_ip = '172.31.7.79'  // replace with JenkinsTestServer IP or DNS
                    def services_to_deploy = (params.SERVICE == 'all') ? services : [params.SERVICE]

                    sshagent(['jenkins-test-server-ssh']) {
                        for (svc in services_to_deploy) {
                            sh """
                            # create folder if not exists
                            ssh -o StrictHostKeyChecking=no ec2-user@${server_ip} "mkdir -p /opt/microservices/${svc}"
                            
                            # copy published files
                            scp -o StrictHostKeyChecking=no -r ${svc}/output/* ec2-user@${server_ip}:/opt/microservices/${svc}/
                            
                            # restart service if systemd exists (optional)
                            ssh -o StrictHostKeyChecking=no ec2-user@${server_ip} "systemctl restart ${svc}.service || echo '${svc} deployed'"
                            """
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
