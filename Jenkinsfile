pipeline {
    agent any

    environment {
        DOTNET_CLI_TELEMETRY_OPTOUT = '1'
        PROJECT_PATH = 'PasswordManager.sln'
        CORE_PROJECT_PATH = 'PasswordManager.Core/PasswordManager.Core.csproj'
        TESTS_PROJECT_PATH = 'PasswordManager.Tests/PasswordManager.Tests.csproj'
        APP_PROJECT_PATH = 'PasswordManager.App/PasswordManager.App.csproj'
        GITHUB_TOKEN = credentials('github-token-id')
        REPO_OWNER = 'romankolosok'
        REPO_NAME = 'PasswordManager'
    }

    stages {
        stage('Checkout') {
            steps {
                checkout scm
            }
        }

        // Restore full solution; build only Core + Tests (Linux-friendly).
        stage('Restore & Build') {
            steps {
                sh "dotnet restore ${env.PROJECT_PATH}"
                sh "dotnet build ${env.CORE_PROJECT_PATH} --configuration Release --no-restore"
                sh "dotnet build ${env.TESTS_PROJECT_PATH} --configuration Release --no-restore"
            }
        }

        // Test Core library only.
        stage('Test & Coverage') {
            steps {
                sh "dotnet test ${env.TESTS_PROJECT_PATH} --configuration Release --no-build --logger 'junit;LogFilePath=test-results.xml' --collect:'XPlat Code Coverage'"
            }
            post {
                always {
                    junit allowEmptyResults: true, testResults: '**/test-results.xml'
                    recordCoverage(
                        tools: [[parser: 'COBERTURA', pattern: '**/coverage.cobertura.xml']],
                        id: 'dotnet-coverage',
                        name: '.NET Coverage'
                    )
                }
            }
        }

        // Build and release the app. Requires PUBLISH_RELEASE=true and a Windows agent (label 'windows') — WPF cannot build on Linux.
        stage('Publish & Release') {
            when {
                allOf {
                    anyOf { branch 'master'; branch 'main' }
                    not { changeRequest() }
                    expression { return env.PUBLISH_RELEASE == 'true' }
                }
            }
            agent { label 'windows' }
            steps {
                checkout scm
                echo "Deploying release from branch: ${env.BRANCH_NAME}"
                bat "dotnet restore ${env.PROJECT_PATH}"
                bat "dotnet publish ${env.APP_PROJECT_PATH} --configuration Release -o publish"
                powershell "Compress-Archive -Path .\\publish\\* -DestinationPath password-manager-release.zip -Force"

                script {
                    def tag = "v1.0.${env.BUILD_NUMBER}"
                    // 1. Create GitHub Release (PowerShell on Windows agent)
                    powershell """
                        \$body = @{ tag_name = '${tag}'; target_commitish = '${env.BRANCH_NAME}'; name = '${tag}'; body = 'Automated release'; draft = \$false; prerelease = \$false } | ConvertTo-Json
                        \$r = Invoke-RestMethod -Uri 'https://api.github.com/repos/${env.REPO_OWNER}/${env.REPO_NAME}/releases' -Method Post -Headers @{ Authorization = "token ${env.GITHUB_TOKEN}"; 'Content-Type' = 'application/json' } -Body \$body
                        \$r.id | Out-File -FilePath release_id.txt -NoNewline
                    """
                    // 2. Upload asset
                    def releaseId = readFile('release_id.txt').trim()
                    powershell """
                        \$uri = "https://uploads.github.com/repos/${env.REPO_OWNER}/${env.REPO_NAME}/releases/${releaseId}/assets?name=password-manager-release.zip"
                        Invoke-RestMethod -Uri \$uri -Method Post -Headers @{ Authorization = "token ${env.GITHUB_TOKEN}"; 'Content-Type' = 'application/zip' } -InFile 'password-manager-release.zip'
                    """
                }
            }
        }
    }

    post {
        always {
            step([$class: 'GitHubCommitStatusSetter', contextSource: [$class: 'DefaultCommitContextSource']])
            cleanWs()
        }
    }
}
